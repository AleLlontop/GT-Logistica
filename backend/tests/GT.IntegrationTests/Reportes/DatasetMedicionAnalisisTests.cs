using System.Diagnostics;
using System.Text.Json;
using GT.Domain.Facturacion;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class DatasetMedicionAnalisisTests
{
    [Fact]
    public async Task DatasetReproducible_ConteosYRelacionesValidos_BaseEliminada()
    {
        var app=new AplicacionAnalisisDePrueba();
        string? nombre=null;
        object? evidencia=null;
        try
        {
            await app.InitializeAsync();
            var previo=await app.ConAlcanceAsync(db=>new DetectorSnapshotAnalisis(db).ConsultarAsync());
            var cronometro=Stopwatch.StartNew();
            await DatosMedicionAnalisis.CrearAsync(app);
            cronometro.Stop();
            using var scope=app.Services.CreateScope();
            var db=scope.ServiceProvider.GetRequiredService<GtDbContext>();
            nombre=db.Database.GetDbConnection().Database;
            Assert.StartsWith("GtLogistica_Test_",nombre);
            var viajes=await db.Viajes.CountAsync();var facturas=await db.Facturas.CountAsync();
            var liquidaciones=await db.Liquidaciones.CountAsync();var movimientos=await db.MovimientosDeCaja.CountAsync();
            Assert.Equal(10000,viajes);Assert.Equal(5000,facturas);Assert.Equal(5000,liquidaciones);Assert.Equal(10000,movimientos);
            Assert.False(await db.Liquidaciones.AnyAsync(l=>l.ImporteTotal!=l.Viajes.Sum(v=>v.Viaje!.Importe)));
            Assert.False(await db.Liquidaciones.AnyAsync(l=>l.ImportePagado!=l.OrdenesDePago.Where(o=>o.Vigente).Sum(o=>o.Importe)));
            Assert.False(await db.Facturas.Where(f=>f.Estado!=EstadoFactura.Anulada).AnyAsync(f=>f.Neto!=db.Viajes.Where(v=>v.FacturaId==f.Id).Sum(v=>v.Importe)));
            var diagnostico=await new DetectorSnapshotAnalisis(db).ConsultarAsync();
            Assert.True(diagnostico.Disponible);Assert.Equal(previo.ReadCommittedSnapshot,diagnostico.ReadCommittedSnapshot);
            var sql=await db.Database.SqlQueryRaw<string>("SELECT CONVERT(nvarchar(128),SERVERPROPERTY('ProductVersion')) AS Value").SingleAsync();
            evidencia=new
            {
                FechaArgentina=DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(-3)),DatosMedicionAnalisis.Semilla,DatosMedicionAnalisis.Anio,
                Conteos=new{viajes,facturas,liquidaciones,movimientos},PreparacionMs=cronometro.ElapsedMilliseconds,
                SdkRequerido="10.0.302",Runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,SqlServer=sql,
                Procesadores=Environment.ProcessorCount,MemoriaDisponibleBytes=GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
                Cpu=LineaDeProc("/proc/cpuinfo","model name")?.Split(':',2).Last().Trim(),
                Ram=LineaDeProc("/proc/meminfo","MemTotal:"),
                Snapshot=diagnostico.Descripcion,RcsiHeredado=previo.ReadCommittedSnapshot,RcsiConservado=previo.ReadCommittedSnapshot==diagnostico.ReadCommittedSnapshot,Consistencia="Snapshot explícito por respuesta",
            };
        }
        finally
        {
            await app.DisposeAsync();
            if(nombre is not null)
            {
                await using var maestra=new SqlConnection(ConfiguracionEntorno.CadenaDeConexionMaestra());
                await maestra.OpenAsync();await using var consulta=maestra.CreateCommand();
                consulta.CommandText="SELECT COUNT(*) FROM sys.databases WHERE name=@nombre";
                consulta.Parameters.AddWithValue("@nombre",nombre);
                Assert.Equal(0,Convert.ToInt32(await consulta.ExecuteScalarAsync()));
                if(evidencia is not null)
                {
                    var carpeta=Path.Combine(Path.GetTempPath(),"gt015-medicion");
                    Directory.CreateDirectory(carpeta);
                    await File.WriteAllTextAsync(Path.Combine(carpeta,"dataset.json"),JsonSerializer.Serialize(new{Entorno=evidencia,BaseEfimeraEliminada=true},new JsonSerializerOptions{WriteIndented=true}));
                }
            }
        }
    }

    // `/proc` sólo existe en Linux (CI y contenedores). En Windows la evidencia va sin CPU ni RAM en vez
    // de hacer fallar un test que mide el dataset, no la máquina.
    private static string? LineaDeProc(string archivo,string prefijo)=>
        File.Exists(archivo)?File.ReadLines(archivo).FirstOrDefault(l=>l.StartsWith(prefijo,StringComparison.Ordinal)):null;
}
