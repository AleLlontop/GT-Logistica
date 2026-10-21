using System.Text.Json;
using GT.Domain.Liquidaciones;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Pagos;
using GT.IntegrationTests.Proveedores;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Reportes;

public class UpgradeSnapshotAnalisisTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task PrepararBaseExistentePreservaOPManualOPPFacturasYMovimientos()
    {
        var efectivo=await EscenarioAnulacion.CrearAsync(app,"efectivo");
        var fletero=await EscenarioAnulacion.CrearAsync(app,"fletero");
        await app.EnLaBaseAsync(async db =>
        {
            var transportista=EscenarioProveedores.Externo("20"+Random.Shared.NextInt64(100000000,999999999));
            var liquidacion=EscenarioProveedores.Pendiente(transportista,121m);
            liquidacion.ImportePagado=30m;
            db.OrdenesDePago.Add(new OrdenDePago { LiquidacionId=0,Liquidacion=liquidacion,FechaPago=new(2026,10,18),Importe=30m,
                UsuarioId=efectivo.UsuarioId,RegistradaEn=new(2026,10,18,15,0,0,DateTimeKind.Utc) });
            await db.SaveChangesAsync();
            var antes=await CapturarAsync(db);
            var detector=new DetectorSnapshotAnalisis(db);
            var estadoAnterior=await detector.ConsultarAsync();
            Assert.False(estadoAnterior.Disponible);
            var nombre=db.Database.GetDbConnection().Database;
            Assert.StartsWith("GtLogistica_Test_",nombre);
            await using var admin=new SqlConnection(ConfiguracionEntorno.CadenaDeConexionMaestra());
            await admin.OpenAsync();
            await using var cmd=admin.CreateCommand();
            cmd.CommandText=PreparacionSnapshotAnalisisTests.LeerSql().Replace("$(Base015)",nombre,StringComparison.Ordinal);
            await cmd.ExecuteNonQueryAsync();
            db.ChangeTracker.Clear();
            Assert.Equal(antes,await CapturarAsync(db));
            var despues=await detector.ConsultarAsync();
            Assert.True(despues.Disponible);
            Assert.Equal(estadoAnterior.ReadCommittedSnapshot,despues.ReadCommittedSnapshot);
            Assert.Equal(1,await db.OrdenesDePago.CountAsync(o=>o.OrdenPagoProveedorId==null));
            Assert.Equal(3,await db.OrdenesDePago.CountAsync(o=>o.OrdenPagoProveedorId==fletero.OrdenId));
            Assert.Single(await db.MovimientosDeCaja.Where(m=>m.OrdenPagoProveedorId==efectivo.OrdenId).ToListAsync());
        });
    }

    private static async Task<string> CapturarAsync(GtDbContext db) => JsonSerializer.Serialize(new
    {
        Migraciones=await db.Database.GetAppliedMigrationsAsync(),
        Facturas=await db.FacturasProveedor.AsNoTracking().OrderBy(f=>f.Id).Select(f=>new {f.Id,f.Numero,f.Neto,f.Iva,f.ImporteTotal,f.Estado,f.LiquidacionId}).ToArrayAsync(),
        Liquidaciones=await db.Liquidaciones.AsNoTracking().OrderBy(l=>l.Id).Select(l=>new {l.Id,l.Numero,l.Estado,l.ImporteTotal,l.ImportePagado,l.Version}).ToArrayAsync(),
        Op=await db.OrdenesDePago.AsNoTracking().OrderBy(o=>o.Id).Select(o=>new {o.Id,o.Numero,o.FechaPago,o.Importe,o.Vigente,o.OrdenPagoProveedorId,o.RevertidaEn}).ToArrayAsync(),
        Opp=await db.OrdenesPagoProveedor.AsNoTracking().OrderBy(o=>o.Id).Select(o=>new {o.Id,o.Numero,o.Fecha,o.ImporteTotal,o.Estado}).ToArrayAsync(),
        Movimientos=await db.MovimientosDeCaja.AsNoTracking().OrderBy(m=>m.Id).Select(m=>new {m.Id,m.CajaId,m.Fecha,m.Tipo,m.Importe,m.OrdenDePagoId,m.OrdenPagoProveedorId}).ToArrayAsync(),
        Resultados=await db.ResultadosOperacionPago.AsNoTracking().OrderBy(r=>r.Id).Select(r=>r.Id).ToArrayAsync(),
    });
}
