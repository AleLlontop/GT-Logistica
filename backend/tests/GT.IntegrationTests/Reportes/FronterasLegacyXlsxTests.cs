using System.Net;
using System.Text.Json;
using GT.IntegrationTests.Caja;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;

namespace GT.IntegrationTests.Reportes;

public class FronterasLegacyXlsxTests
{
    [Theory]
    [InlineData("viajes")]
    [InlineData("facturas")]
    [InlineData("caja")]
    public async Task CeldasRepresentables_TotalPierdeCentavo_RechazoExplicitoYPdfDisponible(string recurso)
    {
        await using var app = new AplicacionDePrueba(); await app.InitializeAsync();
        var cliente = await app.CrearClienteAsync(); string ruta;
        if (recurso == "viajes")
        {
            await app.CrearViajeAsync(cliente.Id, importe: 1000000000000000m);
            await app.CrearViajeAsync(cliente.Id, importe: 0.01m);
            ruta = $"/api/viajes/reporte?clienteId={cliente.Id}";
        }
        else if (recurso == "facturas")
        {
            var hoy = GT.Domain.Choferes.FechaHoyArgentina.Desde(TimeProvider.System.GetUtcNow());
            await app.CrearFacturaAsync(cliente.Id, fecha: hoy.AddDays(-10), vencimientoPago: hoy.AddDays(-1), neto: 1000000000000000m);
            await app.CrearFacturaAsync(cliente.Id, fecha: hoy.AddDays(-10), vencimientoPago: hoy.AddDays(-1), neto: 0.01m);
            ruta = "/api/facturas/vencimientos/reporte?";
        }
        else
        {
            var usuario = await app.UsuarioAsync(); var caja = await app.CrearCajaAsync(usuario.Id);
            await app.CrearMovimientoAsync(caja.Id, usuario.Id, importe: 1000000000000000m);
            await app.CrearMovimientoAsync(caja.Id, usuario.Id, importe: 0.01m);
            ruta = $"/api/movimientos-caja/reporte?cajaId={caja.Id}";
        }
        using var http = await app.CrearClienteAutenticadoAsync();
        using var rechazo = await http.GetAsync(ruta + "&formato=excel");
        Assert.Equal(HttpStatusCode.Conflict, rechazo.StatusCode);
        Assert.Null(rechazo.Content.Headers.ContentDisposition);
        using var json = JsonDocument.Parse(await rechazo.Content.ReadAsStringAsync());
        Assert.Equal("precision_monetaria_xlsx", json.RootElement.GetProperty("codigo").GetString());
        using var pdf = await http.GetAsync(ruta + "&formato=pdf");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
    }
}
