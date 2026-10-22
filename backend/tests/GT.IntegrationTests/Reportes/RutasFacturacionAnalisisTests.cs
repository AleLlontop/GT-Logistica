using System.Net;
using System.Text.Json;
using GT.Domain.Facturacion;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;

namespace GT.IntegrationTests.Reportes;

public class RutasFacturacionAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task DocumentoYFlujoTienenContextosSeparados_PermisosCompuestosSinGestion()
    {
        var cliente = await app.CrearClienteAsync(activo: false);
        var factura = await app.CrearFacturaAsync(cliente.Id, fecha: new(2026, 9, 15), estado: EstadoFactura.Pagada, fechaCobro: new(2026, 10, 10), neto: 100m);
        string[] rutas = ["/api/reportes/facturacion", "/api/reportes/facturacion/cobros", "/api/reportes/facturacion/grupos", "/api/reportes/facturacion/cobros/grupos", "/api/reportes/facturacion/referencias/clientes"];
        foreach(var permisos in new[]{Array.Empty<string>(), new[]{CodigosPermiso.ReportesConsultar}, new[]{CodigosPermiso.FacturacionConsultar}, new[]{CodigosPermiso.ReportesConsultar,CodigosPermiso.FacturacionConsultar}})
        {
            var nombre = "rf_" + Guid.NewGuid().ToString("N")[..10];
            await app.CrearUsuarioConPermisosAsync(nombre, "Reportes.12345678", permisos);
            using var http = await app.CrearClienteAutenticadoAsync(nombre, "Reportes.12345678");
            foreach(var ruta in rutas)
            {
                using var respuesta = await http.GetAsync(ruta + $"?desde=2026-10-01&hasta=2026-10-31&clienteId={cliente.Id}&dimension=cliente");
                Assert.Equal(permisos.Length == 2 ? HttpStatusCode.OK : HttpStatusCode.Forbidden, respuesta.StatusCode);
                if(permisos.Length != 2 || ruta.Contains("grupos") || ruta.Contains("referencias")) continue;
                using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
                var cobro = ruta.EndsWith("cobros");
                Assert.Equal(cobro ? "fechaCobro" : "fechaFactura", json.RootElement.GetProperty("contexto").GetProperty("criterioTemporal").GetString());
                Assert.Equal(cobro ? 1 : 0, json.RootElement.GetProperty("detalle").GetProperty("total").GetInt32());
                if(cobro) Assert.Equal("121.00", json.RootElement.GetProperty("resumen").GetProperty("importeCobrado").GetString());
            }
        }
        using var administrador = await app.CrearClienteAutenticadoAsync();
        using var error = await administrador.GetAsync("/api/reportes/facturacion?desde=2026-10-01&hasta=2026-10-31&estado=desconocido");
        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);
        using var grupoEstado = await administrador.GetAsync("/api/reportes/facturacion/grupos?desde=2026-09-01&hasta=2026-09-30&dimension=estado");
        Assert.Equal(HttpStatusCode.OK, grupoEstado.StatusCode);
        using var grupoInvalido = await administrador.GetAsync("/api/reportes/facturacion/cobros/grupos?desde=2026-10-01&hasta=2026-10-31&dimension=estado");
        Assert.Equal(HttpStatusCode.BadRequest, grupoInvalido.StatusCode);
    }
}
