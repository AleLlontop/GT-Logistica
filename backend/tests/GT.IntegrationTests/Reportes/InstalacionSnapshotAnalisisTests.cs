using System.Net;
using System.Net.Http.Json;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Reportes;

public class InstalacionSnapshotAnalisisTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task BaseNuevaMigrada_PreparaSnapshot_SinCambiarOperacionesNiLegacy()
    {
        await app.EnLaBaseAsync(async db =>
        {
            Assert.Empty(await db.Database.GetPendingMigrationsAsync());
            Assert.Contains("20261018164612_Modulo14GestionPagos",await db.Database.GetAppliedMigrationsAsync());
            Assert.Empty(await db.Viajes.ToListAsync());
            var detector=new DetectorSnapshotAnalisis(db);
            var antes=await detector.ConsultarAsync();
            Assert.False(antes.Disponible);
            var nombre=db.Database.GetDbConnection().Database;
            Assert.StartsWith("GtLogistica_Test_",nombre);
            await using var admin=new SqlConnection(ConfiguracionEntorno.CadenaDeConexionMaestra());
            await admin.OpenAsync();
            await using var cmd=admin.CreateCommand();
            cmd.CommandText=PreparacionSnapshotAnalisisTests.LeerSql().Replace("$(Base015)",nombre,StringComparison.Ordinal);
            await cmd.ExecuteNonQueryAsync();
            var despues=await detector.ConsultarAsync();
            Assert.True(despues.Disponible);
            Assert.Equal(antes.ReadCommittedSnapshot,despues.ReadCommittedSnapshot);
        });
        using var cliente=await app.CrearClienteAutenticadoAsync();
        using var banco=await cliente.PostAsJsonAsync("/api/pagos/bancos",new {nombre="Banco prueba provisión015"});
        Assert.Equal(HttpStatusCode.Created,banco.StatusCode);
        foreach(var ruta in new[]{"/api/viajes/reporte","/api/vencimientos/reporte","/api/flota/vencimientos/reporte","/api/facturas/vencimientos/reporte","/api/movimientos-caja/reporte"})
        {
            using var respuesta=await cliente.GetAsync(ruta+"?formato=excel");
            Assert.Equal(HttpStatusCode.OK,respuesta.StatusCode);
            Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",respuesta.Content.Headers.ContentType?.MediaType);
        }
    }
}
