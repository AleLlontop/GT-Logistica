using System.Net;
using GT.Application.Reportes.Analisis;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class ReadinessAnalisisOffTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task OffNoHabilitaLecturasNuevas_PeroLegacySigueOperativo()
    {
        using var scope=app.Services.CreateScope();
        var readiness=scope.ServiceProvider.GetRequiredService<ReadinessAnalisisReportes>();
        await Assert.ThrowsAsync<AnalisisNoDisponibleException>(()=>readiness.AsegurarAsync());
        using var cliente=await app.CrearClienteAutenticadoAsync();
        foreach (var ruta in new[]{"/api/reportes/facturacion", "/api/reportes/facturacion/cobros", "/api/reportes/facturacion/grupos", "/api/reportes/facturacion/cobros/grupos", "/api/reportes/facturacion/referencias/clientes", "/api/reportes/liquidaciones", "/api/reportes/liquidaciones/pagos", "/api/reportes/liquidaciones/grupos", "/api/reportes/liquidaciones/pagos/grupos", "/api/reportes/liquidaciones/referencias/transportistas", "/api/reportes/caja", "/api/reportes/caja/grupos", "/api/reportes/caja/referencias/cajas", "/api/reportes/flota", "/api/reportes/flota/grupos", "/api/reportes/flota/referencias/vehiculos", "/api/reportes/flota/referencias/tipos", "/api/reportes/flota/referencias/transportistas"})
        {
            using var respuesta = await cliente.GetAsync(ruta + "?desde=2026-10-01&hasta=2026-10-31&dimension=" + (ruta.Contains("liquidaciones") ? "transportista" : ruta.Contains("/caja") || ruta.Contains("/flota") ? "tipo" : "cliente"));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
            Assert.Contains("analisis_no_disponible", await respuesta.Content.ReadAsStringAsync());
        }

        foreach(var ruta in new[]{"/api/reportes/vencimientos/choferes", "/api/reportes/vencimientos/flota", "/api/reportes/vencimientos/facturas", "/api/reportes/dashboard?desde=2026-10-01&hasta=2026-10-31"})
        {
            using var respuesta=await cliente.GetAsync(ruta);
            Assert.Equal(HttpStatusCode.ServiceUnavailable,respuesta.StatusCode);
            Assert.Contains("analisis_no_disponible",await respuesta.Content.ReadAsStringAsync());
        }
        foreach (var recurso in RutasExportacionAnalisisTests.Recursos)
        {
            using var respuesta = await cliente.GetAsync(RutasExportacionAnalisisTests.Url(recurso, "excel"));
            Assert.Equal(HttpStatusCode.ServiceUnavailable, respuesta.StatusCode);
            Assert.Contains("analisis_no_disponible", await respuesta.Content.ReadAsStringAsync());
            Assert.Null(respuesta.Content.Headers.ContentDisposition);
        }
        foreach(var ruta in new[]{"/api/viajes/reporte","/api/vencimientos/reporte","/api/flota/vencimientos/reporte","/api/facturas/vencimientos/reporte","/api/movimientos-caja/reporte"})
        {
            using var resultado=await cliente.GetAsync(ruta+"?formato=excel");
            Assert.Equal(HttpStatusCode.OK,resultado.StatusCode);
        }
    }
}

public class ReadinessAnalisisOnTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task OnSeResuelveDesdeDIYEstaDisponible()
    {
        using var scope=app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ReadinessAnalisisReportes>().AsegurarAsync();
    }
}
