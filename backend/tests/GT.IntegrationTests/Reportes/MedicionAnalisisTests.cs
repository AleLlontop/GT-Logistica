using System.Data;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

internal sealed record MuestraAnalisis(string Recurso,int? Tamanio,int Consultas,long Milisegundos,int Filas);
internal sealed record MedicionRecursoAnalisis(string Recurso,long[] MuestrasMs,int Consultas20,int? Consultas200,int Filas20,int? Filas200);

internal static class ArnesMedicionAnalisis
{
    internal static readonly string[] Recursos=["dashboard","viajes","facturacion","facturacion/cobros","liquidaciones","liquidaciones/pagos","caja","flota","vencimientos/choferes","vencimientos/flota","vencimientos/facturas"];
    public static async Task EjecutarAsync(AplicacionMedicionAnalisis app,string carpeta,int repeticiones=20)
    {
        Directory.CreateDirectory(carpeta);
        await DatosMedicionAnalisis.CrearAsync(app,cantidadVehiculos:250);
        await DocumentacionMedicionAnalisis.CrearAsync(app);
        using var scope=app.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<GtDbContext>();
        Assert.Equal(10000,await db.Viajes.CountAsync());Assert.Equal(5000,await db.Facturas.CountAsync());
        Assert.Equal(5000,await db.Liquidaciones.CountAsync());Assert.Equal(10000,await db.MovimientosDeCaja.CountAsync());
        Assert.Equal(250,await db.Vehiculos.CountAsync());Assert.Equal(200,await db.Choferes.CountAsync());
        var nombre=db.Database.GetDbConnection().Database;
        var comandos=new Dictionary<string,(string Recurso,ComandoMedicion Comando)>(StringComparer.Ordinal);
        var resultados=new List<MedicionRecursoAnalisis>();
        using var http=await app.CrearClienteAutenticadoAsync();
        async Task<MuestraAnalisis> Consultar(string recurso,int? tamanio,bool capturar)
        {
            var url="/api/reportes/"+recurso;
            if(!recurso.StartsWith("vencimientos",StringComparison.Ordinal))url+="?desde=2026-01-01&hasta=2026-12-31";
            if(tamanio is { } filas)url+=(url.Contains('?')?"&":"?")+"tamanioPagina="+filas;
            app.Captura.Iniciar();
            var reloj=Stopwatch.StartNew();
            using var respuesta=await http.GetAsync(url);
            var contenido=await respuesta.Content.ReadAsByteArrayAsync();
            reloj.Stop();app.Captura.Activa=false;
            Assert.Equal(HttpStatusCode.OK,respuesta.StatusCode);
            var consultas=app.Captura.Comandos.ToArray();
            Assert.All(consultas,c=>Assert.Equal(IsolationLevel.Snapshot,c.Aislamiento));
            if(capturar)foreach(var comando in consultas)comandos.TryAdd(comando.Clave,(recurso,comando));
            using var json=JsonDocument.Parse(contenido);
            var cantidad=json.RootElement.TryGetProperty("detalle",out var detalle)?detalle.GetProperty("items").GetArrayLength():0;
            return new(recurso,tamanio,consultas.Length,reloj.ElapsedMilliseconds,cantidad);
        }
        foreach(var recurso in Recursos)
        {
            int? tamanio=recurso=="dashboard"?null:20;
            await Consultar(recurso,tamanio,false);await Consultar(recurso,tamanio,false);
            var muestras=new List<MuestraAnalisis>();
            for(var i=0;i<repeticiones;i++)muestras.Add(await Consultar(recurso,tamanio,i==0));
            MuestraAnalisis? grande=null;
            if(tamanio.HasValue)
            {
                grande=await Consultar(recurso,200,true);
                Assert.Equal(muestras[0].Consultas,grande.Consultas);
                Assert.Equal(20,muestras[0].Filas);Assert.Equal(200,grande.Filas);
            }
            resultados.Add(new(recurso,muestras.Select(m=>m.Milisegundos).ToArray(),muestras[0].Consultas,grande?.Consultas,muestras[0].Filas,grande?.Filas));
            await File.WriteAllTextAsync(Path.Combine(carpeta,"tiempos.json"),JsonSerializer.Serialize(new{CalentamientoPorRecurso=2,Repeticiones=repeticiones,UnaSesionActiva=true,Periodo="2026-01-01/2026-12-31",Vencimientos="Situación actual, sin período",Resultados=resultados},new JsonSerializerOptions{WriteIndented=true}));
        }
        var planes=new List<PlanMedido>();
        foreach(var captura in comandos.Values)
        {
            using var limite=new CancellationTokenSource(TimeSpan.FromSeconds(90));
            planes.Add(await LecturasYPlanesAnalisis.MedirAsync(nombre,captura.Recurso,captura.Comando,Path.Combine(carpeta,"planes"),limite.Token));
            await File.WriteAllTextAsync(Path.Combine(carpeta,"planes-io.json"),JsonSerializer.Serialize(planes,new JsonSerializerOptions{WriteIndented=true}));
        }
        Assert.NotEmpty(planes);Assert.True(planes.Sum(p=>p.LecturasLogicas)>0);
        await File.WriteAllTextAsync(Path.Combine(carpeta,"finalizada.json"),JsonSerializer.Serialize(new{Finalizada=true,Recursos=resultados.Count,Planes=planes.Count,SinNMasUno=true,IndicesNuevos=false}));
    }
}

public class MedicionAnalisisTests
{
    [Fact]
    public async Task BaselineIndicesExistentes_Consultas20vs200_TiemposYPlanesReales()
    {
        var app=new AplicacionMedicionAnalisis();string? nombre=null;
        try
        {
            await app.InitializeAsync();
            nombre=await app.ConAlcanceAsync(db=>Task.FromResult(db.Database.GetDbConnection().Database));
            await ArnesMedicionAnalisis.EjecutarAsync(app,"/tmp/gt015-medicion/baseline");
        }
        finally
        {
            await app.DisposeAsync();
            if(nombre is not null)
            {
                await using var maestra=new SqlConnection(ConfiguracionEntorno.CadenaDeConexionMaestra());await maestra.OpenAsync();
                await using var cmd=maestra.CreateCommand();cmd.CommandText="SELECT COUNT(*) FROM sys.databases WHERE name=@nombre";cmd.Parameters.AddWithValue("@nombre",nombre);
                Assert.Equal(0,Convert.ToInt32(await cmd.ExecuteScalarAsync()));
                Directory.CreateDirectory("/tmp/gt015-medicion/baseline");await File.WriteAllTextAsync("/tmp/gt015-medicion/baseline/limpieza.json","{\"BaseEfimeraEliminada\":true}");
            }
        }
    }
}
