using System.Net;
using GT.Api.Reportes;
using GT.Application.Reportes.Analisis;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class SnapshotAnalisisOffTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Theory]
    [InlineData("dashboard")] [InlineData("tabla")] [InlineData("grupos")] [InlineData("exportacion")]
    public async Task NingunaLecturaNuevaSeEjecutaSinSnapshot(string consumidor)
    {
        using var scope=app.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<GtDbContext>();
        var coordinador=scope.ServiceProvider.GetRequiredService<CoordinadorFotoAnalisis>();
        var invocaciones=0;
        var respuesta=await SerializadorAnalisisReportes.EjecutarAsync(async ()=>
        {
            var datos=await coordinador.LeerAsync((ctx,ct)=>
            {
                invocaciones++;
                return Task.FromResult(consumidor);
            });
            return SerializadorAnalisisReportes.Json(datos);
        });
        Assert.Equal(0,invocaciones);
        Assert.Equal(503,Assert.IsAssignableFrom<IStatusCodeHttpResult>(respuesta).StatusCode);
        Assert.Null(db.Database.CurrentTransaction);
    }

    [Fact]
    public async Task LasCincoRutasLegacyNoUsanReadinessNuevo()
    {
        using var cliente=await app.CrearClienteAutenticadoAsync();
        foreach(var ruta in new[]{"/api/viajes/reporte","/api/vencimientos/reporte","/api/flota/vencimientos/reporte","/api/facturas/vencimientos/reporte","/api/movimientos-caja/reporte"})
        {
            using var respuesta=await cliente.GetAsync(ruta+"?formato=excel");
            Assert.Equal(HttpStatusCode.OK,respuesta.StatusCode);
        }
    }
}
