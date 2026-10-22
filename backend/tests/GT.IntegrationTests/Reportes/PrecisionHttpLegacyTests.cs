using System.Net;
using System.Text.Json;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;

namespace GT.IntegrationTests.Reportes;

public class PrecisionHttpLegacyTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Ruta012Devuelve409DePrecision_ConsultaYPdfSiguenDisponibles()
    {
        var cliente = await app.CrearClienteAsync();
        await app.CrearViajeAsync(cliente.Id, importe: 9999999999999999.99m);
        using var http = await app.CrearClienteAutenticadoAsync();
        using var respuesta = await http.GetAsync($"/api/viajes/reporte?formato=excel&clienteId={cliente.Id}");
        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Null(respuesta.Content.Headers.ContentDisposition);
        Assert.Equal("application/json", respuesta.Content.Headers.ContentType?.MediaType);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal("precision_monetaria_xlsx", json.RootElement.GetProperty("codigo").GetString());
        Assert.Equal("No podemos exportar este reporte a Excel sin perder centavos. Consultalo en pantalla o generá el PDF.", json.RootElement.GetProperty("mensaje").GetString());
        using var pdf = await http.GetAsync($"/api/viajes/reporte?formato=pdf&clienteId={cliente.Id}");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        using var consulta = await http.GetAsync($"/api/viajes?clienteId={cliente.Id}");
        Assert.Equal(HttpStatusCode.OK, consulta.StatusCode);
    }
}
