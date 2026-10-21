using System.Net;
using System.Text.Json;
using ClosedXML.Excel;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;

namespace GT.IntegrationTests.Reportes;

public class RutasExportacionAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    internal static readonly string[] Recursos = ["viajes", "facturacion", "facturacion/cobros", "liquidaciones", "liquidaciones/pagos", "caja", "flota", "vencimientos/choferes", "vencimientos/flota", "vencimientos/facturas"];
    internal static string Url(string recurso, string formato) => "/api/reportes/" + recurso + "/exportacion?formato=" + formato +
        (recurso.StartsWith("vencimientos") ? "" : "&desde=2026-10-01&hasta=2026-10-31");

    [Fact]
    public async Task DiezRutasOfrecenAmbosFormatos_ConCabecerasYContextosCorrectos()
    {
        using var http = await app.CrearClienteAutenticadoAsync();
        foreach (var recurso in Recursos)
        foreach (var formato in new[] { "pdf", "excel" })
        {
            using var respuesta = await http.GetAsync(Url(recurso, formato).Replace("2026-10", "2025-10", StringComparison.Ordinal));
            Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
            Assert.True(respuesta.Headers.CacheControl?.NoStore);
            Assert.Equal("nosniff", respuesta.Headers.GetValues("X-Content-Type-Options").Single());
            Assert.Equal(formato == "pdf" ? "inline" : "attachment", respuesta.Content.Headers.ContentDisposition?.DispositionType);
            Assert.EndsWith(formato == "pdf" ? ".pdf" : ".xlsx", respuesta.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
            Assert.Equal(formato == "pdf" ? "application/pdf" : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", respuesta.Content.Headers.ContentType?.MediaType);
            var bytes = await respuesta.Content.ReadAsByteArrayAsync();
            if (formato == "pdf") Assert.Equal("%PDF"u8.ToArray(), bytes[..4]);
            else
            {
                using var memoria = new MemoryStream(bytes); using var libro = new XLWorkbook(memoria);
                var hoja = libro.Worksheets.First();
                Assert.Contains("Generado por", hoja.Cell(4, 1).GetString());
                Assert.Contains(recurso.StartsWith("vencimientos") ? "Situación actual" : "01/10/2025", hoja.Cell(2, 1).GetString());
            }
        }
    }

    [Theory]
    [InlineData("PDF")]
    [InlineData("xlsx")]
    [InlineData("")]
    public async Task FormatoInvalido400_SinArchivo(string formato)
    {
        using var http = await app.CrearClienteAutenticadoAsync();
        using var respuesta = await http.GetAsync(Url("viajes", formato));
        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Null(respuesta.Content.Headers.ContentDisposition);
    }

    [Fact]
    public async Task ViajesCompletoDesdePagina2_FiltroAplicadoYPrecision409_PdfDisponible()
    {
        var cliente = await app.CrearClienteAsync();
        for (var i = 0; i < 21; i++) await app.CrearViajeAsync(cliente.Id, new(2026, 10, 18), importe: 1.01m);
        using var http = await app.CrearClienteAutenticadoAsync();
        using var respuesta = await http.GetAsync(Url("viajes", "excel") + $"&clienteId={cliente.Id}&pagina=2&tamanioPagina=1");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using (var memoria = new MemoryStream(await respuesta.Content.ReadAsByteArrayAsync()))
        using (var libro = new XLWorkbook(memoria))
        {
            var hoja = libro.Worksheets.First();
            Assert.Equal("Filas incluidas: 21", hoja.Cell(3, 1).GetString());
            Assert.All(Enumerable.Range(7, 21), fila => Assert.Equal(1.01m, hoja.Cell(fila, 10).GetValue<decimal>()));
        }
        var extremo = await app.CrearClienteAsync();
        await app.CrearViajeAsync(extremo.Id, new(2026, 10, 18), importe: 9999999999999999.99m);
        using var rechazo = await http.GetAsync(Url("viajes", "excel") + $"&clienteId={extremo.Id}");
        Assert.Equal(HttpStatusCode.Conflict, rechazo.StatusCode);
        Assert.Null(rechazo.Content.Headers.ContentDisposition);
        using var json = JsonDocument.Parse(await rechazo.Content.ReadAsStringAsync());
        Assert.Equal("precision_monetaria_xlsx", json.RootElement.GetProperty("codigo").GetString());
        using var pdf = await http.GetAsync(Url("viajes", "pdf") + $"&clienteId={extremo.Id}");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
    }

    [Fact]
    public async Task TresPermisosIndependientes_ConsultaNoImplicaEmitir()
    {
        var casos = new[]
        {
            new[] { CodigosPermiso.ReportesConsultar, CodigosPermiso.ViajesConsultar },
            new[] { CodigosPermiso.ReportesEmitir, CodigosPermiso.ViajesConsultar },
            new[] { CodigosPermiso.ReportesConsultar, CodigosPermiso.ReportesEmitir },
        };
        foreach (var permisos in casos)
        {
            var nombre = "exp_" + Guid.NewGuid().ToString("N")[..10];
            await app.CrearUsuarioConPermisosAsync(nombre, "Reportes.12345678", permisos);
            using var http = await app.CrearClienteAutenticadoAsync(nombre, "Reportes.12345678");
            using var respuesta = await http.GetAsync(Url("viajes", "pdf"));
            Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
            Assert.Null(respuesta.Content.Headers.ContentDisposition);
        }
    }
}
