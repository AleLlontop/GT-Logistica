using System.Net;
using System.Text.Json;
using GT.Domain.Usuarios;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Facturacion;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class PermisosExportacionAnalisisTests(AplicacionObservadaAnalisis app) : IClassFixture<AplicacionObservadaAnalisis>
{
    private static string Fuente(string recurso) => recurso switch
    {
        "viajes" => CodigosPermiso.ViajesConsultar,
        "facturacion" or "facturacion/cobros" or "vencimientos/facturas" => CodigosPermiso.FacturacionConsultar,
        "liquidaciones" or "liquidaciones/pagos" => CodigosPermiso.LiquidacionesConsultar,
        "caja" => CodigosPermiso.CajaConsultar, "flota" => CodigosPermiso.ReportesFlotaConsultar,
        "vencimientos/choferes" => CodigosPermiso.ChoferesVencimientosConsultar,
        "vencimientos/flota" => CodigosPermiso.FlotaVencimientosConsultar,
        _ => throw new ArgumentOutOfRangeException(nameof(recurso)),
    };

    [Fact]
    public async Task InventarioDeDiezRutas_MatrizCompleta_RevocacionYSinConsultasDenegadas()
    {
        var recursos = RutasExportacionAnalisisTests.Recursos;
        var inventario = app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Select(e => e.RoutePattern.RawText).Where(p => p?.StartsWith("/api/reportes/", StringComparison.Ordinal) == true && p.EndsWith("/exportacion", StringComparison.Ordinal)).Order().ToArray();
        Assert.Equal(recursos.Select(r => "/api/reportes/" + r + "/exportacion").Order(), inventario);
        using (var anonimo = app.CrearCliente())
            foreach (var recurso in recursos)
            foreach (var formato in new[] { "pdf", "excel" })
                using (var respuesta = await anonimo.GetAsync(RutasExportacionAnalisisTests.Url(recurso, formato)))
                    Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        var fuentes = recursos.Select(Fuente).Distinct().ToArray();
        List<string[]> perfiles = [[], [CodigosPermiso.ReportesConsultar], [CodigosPermiso.ReportesEmitir],
            [CodigosPermiso.ReportesConsultar, CodigosPermiso.ReportesEmitir],
            [..fuentes, CodigosPermiso.ReportesEmitir], [CodigosPermiso.ReportesConsultar, ..fuentes],
            [CodigosPermiso.ReportesConsultar, CodigosPermiso.ReportesEmitir, ..fuentes]];
        perfiles.AddRange(fuentes.Select(f => new[] { CodigosPermiso.ReportesConsultar, CodigosPermiso.ReportesEmitir, f }));
        perfiles.AddRange(fuentes.Select(f => new[] { CodigosPermiso.ReportesConsultar, CodigosPermiso.ReportesEmitir }.Concat(fuentes.Where(p => p != f)).ToArray()));
        foreach (var permisos in perfiles)
        {
            var nombre = "exp_mat_" + Guid.NewGuid().ToString("N")[..8];
            var usuario = await app.CrearUsuarioConPermisosAsync(nombre, "Reportes.12345678", permisos);
            using var http = await app.CrearClienteAutenticadoAsync(nombre, "Reportes.12345678");
            foreach (var recurso in recursos)
            foreach (var formato in new[] { "pdf", "excel" })
            {
                var permitido = permisos.Contains(CodigosPermiso.ReportesConsultar) && permisos.Contains(CodigosPermiso.ReportesEmitir) && permisos.Contains(Fuente(recurso));
                app.Sonda.Iniciar();
                using var respuesta = await http.GetAsync(RutasExportacionAnalisisTests.Url(recurso, formato));
                app.Sonda.Activa = false;
                Assert.Equal(permitido ? HttpStatusCode.OK : HttpStatusCode.Forbidden, respuesta.StatusCode);
                if (!permitido)
                {
                    Assert.Null(respuesta.Content.Headers.ContentDisposition);
                    using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
                    Assert.Equal("No tenés permiso para consultar o emitir este reporte. Pedíselo a quien administra el sistema.", json.RootElement.GetProperty("mensaje").GetString());
                    Assert.DoesNotContain(app.Sonda.Comandos, c => new[] { "[Viajes]", "[Clientes]", "[Transportistas]", "[Vehiculos]", "[Facturas]", "[Liquidaciones]", "[OrdenesDePago]", "[Cajas]", "[MovimientosDeCaja]", "[Documentaciones]", "[DocumentacionesVehiculo]" }.Any(c.Sql.Contains));
                }
            }
            if (permisos.Length == fuentes.Length + 2)
            {
                foreach (var lateral in new[] { "/api/flota/vehiculos", "/api/choferes", "/api/pagos/bancos" })
                    using (var respuesta = await http.GetAsync(lateral)) Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
                await app.EnLaBaseAsync(async db =>
                {
                    var rol = await db.Roles.Include(r => r.Permisos).SingleAsync(r => r.Id == usuario.Roles.Single().Id);
                    rol.Permisos.Clear(); await db.SaveChangesAsync();
                });
                foreach (var recurso in recursos)
                foreach (var formato in new[] { "pdf", "excel" })
                    using (var respuesta = await http.GetAsync(RutasExportacionAnalisisTests.Url(recurso, formato)))
                        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
            }
        }
    }
}
