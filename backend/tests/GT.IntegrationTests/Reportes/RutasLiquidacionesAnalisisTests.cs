using System.Net;
using System.Text.Json;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Pagos;

namespace GT.IntegrationTests.Reportes;

public class RutasLiquidacionesAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task MesesYFlujosContextualizados_OpOppSeparadas_PermisosIndependientes()
    {
        var pago = await EscenarioAnulacion.CrearAsync(app, "fletero");
        string[] rutas = ["/api/reportes/liquidaciones", "/api/reportes/liquidaciones/pagos", "/api/reportes/liquidaciones/grupos", "/api/reportes/liquidaciones/pagos/grupos", "/api/reportes/liquidaciones/referencias/transportistas"];
        foreach(var permisos in new[]{Array.Empty<string>(), new[]{CodigosPermiso.ReportesConsultar}, new[]{CodigosPermiso.LiquidacionesConsultar}, new[]{CodigosPermiso.ReportesConsultar,CodigosPermiso.LiquidacionesConsultar}})
        {
            var nombre = "rl_" + Guid.NewGuid().ToString("N")[..10];
            await app.CrearUsuarioConPermisosAsync(nombre, "Reportes.12345678", permisos);
            using var http = await app.CrearClienteAutenticadoAsync(nombre, "Reportes.12345678");
            foreach(var ruta in rutas)
            {
                using var respuesta = await http.GetAsync(ruta + "?desde=2026-09-15&hasta=2026-10-31&dimension=transportista");
                Assert.Equal(permisos.Length == 2 ? HttpStatusCode.OK : HttpStatusCode.Forbidden, respuesta.StatusCode);
                if(permisos.Length != 2 || ruta.Contains("grupos") || ruta.Contains("referencias")) continue;
                using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
                var flujo = ruta.EndsWith("pagos");
                var contexto = json.RootElement.GetProperty("contexto");
                Assert.Equal(flujo ? "fechaPago" : "periodoLiquidado", contexto.GetProperty("criterioTemporal").GetString());
                if(!flujo)
                {
                    Assert.Equal("2026-09", contexto.GetProperty("periodoLiquidadoDesde").GetString());
                    Assert.Equal("2026-10", contexto.GetProperty("periodoLiquidadoHasta").GetString());
                }
                else
                {
                    Assert.Equal("363.00", json.RootElement.GetProperty("resumen").GetProperty("importePagadoVigente").GetString());
                    var filas = json.RootElement.GetProperty("detalle").GetProperty("items").EnumerateArray().ToArray();
                    Assert.Equal(3, filas.Length);
                    Assert.All(filas, f =>
                    {
                        Assert.Equal(pago.OrdenId, f.GetProperty("oppId").GetInt32());
                        Assert.Equal("opp", f.GetProperty("origen").GetString());
                        Assert.StartsWith("OP-", f.GetProperty("numero").GetString());
                        Assert.StartsWith("OPP-", f.GetProperty("oppNumero").GetString());
                    });
                }
            }
            using var manual = await http.GetAsync("/api/liquidaciones/" + pago.Liquidaciones[0]);
            if(permisos.Length != 2 && !permisos.Contains(CodigosPermiso.LiquidacionesConsultar)) Assert.Equal(HttpStatusCode.Forbidden, manual.StatusCode);
        }
        using var administrador = await app.CrearClienteAutenticadoAsync();
        foreach(var filtro in new[]{"vigentes", "revertidos", "todos"})
        {
            using var respuesta = await administrador.GetAsync("/api/reportes/liquidaciones/pagos?desde=2026-10-01&hasta=2026-10-31&vigencia=" + filtro);
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        }
        using var error = await administrador.GetAsync("/api/reportes/liquidaciones/pagos?desde=2026-10-01&hasta=2026-10-31&vigencia=invalida");
        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        using var estados = await administrador.GetAsync("/api/reportes/liquidaciones/grupos?desde=2026-10-01&hasta=2026-10-31&dimension=estado");
        Assert.Equal(HttpStatusCode.OK, estados.StatusCode);
    }
}
