using System.Net.Http.Json;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Flota;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Reportes;

/// <summary>
/// Paridad de los tres paneles de vencimientos con sus reportes (US2, SC-002).
///
/// Lo que se verifica es que cada reporte traiga <b>las mismas filas y en el mismo orden por
/// urgencia</b> que su panel. Los tres llaman a su caso de uso tal cual está —ya devuelven la lista
/// entera—, así que el orden se hereda y no se replica: **Choferes, Flota y Facturación no cambian ni
/// una línea** (research §3).
/// </summary>
public class ReportesDeVencimientosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    // ── Choferes ────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Choferes_ElReporteTraeLasMismasFilasYEnElMismoOrden()
    {
        var administrador = await app.CrearClienteAutenticadoAsync();
        var tipo = await app.CrearTipoDocumentacionAsync("Licencia del reporte");

        // Tres urgencias distintas: lo vencido hace más tiempo va primero.
        var primero = await app.CrearChoferCompletoAsync(DatosDePruebaViajesReportes.Semilla());
        var segundo = await app.CrearChoferCompletoAsync(DatosDePruebaViajesReportes.Semilla());
        var tercero = await app.CrearChoferCompletoAsync(DatosDePruebaViajesReportes.Semilla());

        await app.CrearDocumentoAsync(segundo.Id, tipo.Id, diasHastaVencimiento: 5);
        await app.CrearDocumentoAsync(primero.Id, tipo.Id, diasHastaVencimiento: -30);
        await app.CrearDocumentoAsync(tercero.Id, tipo.Id, diasHastaVencimiento: -2);

        var panel = await administrador.GetFromJsonAsync<List<AlertaDeChofer>>("/api/vencimientos");

        var filas = await ReporteViajesTests.FilasDelExcelAsync(
            administrador, "/api/vencimientos/reporte?formato=excel");

        Assert.Equal(panel!.Count, filas.Count);

        Assert.Equal(
            [.. panel.Select(alerta => $"{alerta.Apellido}, {alerta.Nombre}")],
            [.. filas.Select(fila => (string)fila[0])]);
    }

    /// <summary>La palabra del panel, no el código del JSON (FR-010).</summary>
    [Fact]
    public async Task Choferes_ElEstadoVaConLaPalabraDelPanel()
    {
        var administrador = await app.CrearClienteAutenticadoAsync();
        var tipo = await app.CrearTipoDocumentacionAsync("Licencia de la palabra");
        var chofer = await app.CrearChoferCompletoAsync(DatosDePruebaViajesReportes.Semilla());

        await app.CrearDocumentoAsync(chofer.Id, tipo.Id, diasHastaVencimiento: -3);

        var filas = await ReporteViajesTests.FilasDelExcelAsync(
            administrador, "/api/vencimientos/reporte?formato=excel");

        Assert.All(filas, fila => Assert.DoesNotContain("proximaAvencer", (string)fila[4]));
        Assert.Contains(filas, fila => (string)fila[4] is "Vencida" or "Próxima a vencer");
    }

    /// <summary>Su encabezado dice siempre <c>Sin filtros aplicados</c>: el panel no tiene filtros.</summary>
    [Fact]
    public async Task Choferes_ElEncabezadoDiceQueNoHayFiltros()
    {
        var administrador = await app.CrearClienteAutenticadoAsync();

        var texto = await EncabezadoDelExcelAsync(administrador, "/api/vencimientos/reporte?formato=excel");

        Assert.Equal("Reporte de vencimientos de choferes", texto[0]);
        Assert.Equal("Sin filtros aplicados", texto[1]);
    }

    // ── Flota ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Flota_ElReporteTraeLasMismasFilasYEnElMismoOrden()
    {
        var administrador = await app.CrearClienteAutenticadoAsync();
        var transportista = await app.CrearTransportistaAsync();
        var tipoVehiculo = await app.CrearTipoVehiculoAsync();
        var tipo = await app.CrearTipoDocumentacionDeVehiculoAsync("Seguro del reporte");

        var uno = await app.CrearVehiculoAsync(tipoVehiculo.Id, transportista.Id);
        var dos = await app.CrearVehiculoAsync(tipoVehiculo.Id, transportista.Id);
        var tres = await app.CrearVehiculoAsync(tipoVehiculo.Id, transportista.Id);

        await app.CrearDocumentoVehiculoAsync(dos.Id, tipo.Id, diasHastaVencimiento: 4);
        await app.CrearDocumentoVehiculoAsync(uno.Id, tipo.Id, diasHastaVencimiento: -20);
        await app.CrearDocumentoVehiculoAsync(tres.Id, tipo.Id, diasHastaVencimiento: -1);

        var panel = await administrador.GetFromJsonAsync<List<AlertaDeFlota>>("/api/flota/vencimientos");

        var filas = await ReporteViajesTests.FilasDelExcelAsync(
            administrador, "/api/flota/vencimientos/reporte?formato=excel");

        Assert.Equal(panel!.Count, filas.Count);

        Assert.Equal(
            [.. panel.Select(alerta => alerta.Patente)],
            [.. filas.Select(fila => (string)fila[0])]);
    }

    [Fact]
    public async Task Flota_ElEncabezadoDiceQueNoHayFiltros()
    {
        var administrador = await app.CrearClienteAutenticadoAsync();

        var texto = await EncabezadoDelExcelAsync(
            administrador, "/api/flota/vencimientos/reporte?formato=excel");

        Assert.Equal("Reporte de vencimientos de flota", texto[0]);
        Assert.Equal("Sin filtros aplicados", texto[1]);
    }

    // ── Facturas ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El panel de facturas puede estar vacío en una base recién sembrada, y **eso también es paridad**:
    /// el reporte trae exactamente las filas que el panel muestra.
    /// </summary>
    [Fact]
    public async Task Facturas_ElReporteTraeLasMismasFilasQueElPanel()
    {
        var administrador = await app.CrearClienteAutenticadoAsync();

        var panel = await administrador.GetFromJsonAsync<List<FilaDeVencimientoDeFactura>>(
            "/api/facturas/vencimientos");

        var filas = await ReporteViajesTests.FilasDelExcelAsync(
            administrador, "/api/facturas/vencimientos/reporte?formato=excel");

        Assert.Equal(panel!.Count, filas.Count);

        Assert.Equal(
            [.. panel.Select(fila => fila.NumeroComprobante)],
            [.. filas.Select(fila => (string)fila[1])]);
    }

    [Fact]
    public async Task Facturas_ElEncabezadoDiceQueNoHayFiltros()
    {
        var administrador = await app.CrearClienteAutenticadoAsync();

        var texto = await EncabezadoDelExcelAsync(
            administrador, "/api/facturas/vencimientos/reporte?formato=excel");

        Assert.Equal("Reporte de vencimientos de facturas", texto[0]);
        Assert.Equal("Sin filtros aplicados", texto[1]);
    }

    // ── Andamio ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>Las cuatro primeras filas del <c>.xlsx</c>: el encabezado de FR-006.</summary>
    internal static async Task<string[]> EncabezadoDelExcelAsync(HttpClient cliente, string ruta)
    {
        var respuesta = await cliente.GetAsync(ruta);
        respuesta.EnsureSuccessStatusCode();

        using var memoria = new MemoryStream(await respuesta.Content.ReadAsByteArrayAsync());
        using var libro = new ClosedXML.Excel.XLWorkbook(memoria);
        var hoja = libro.Worksheets.First();

        return [.. Enumerable.Range(1, 4).Select(fila => hoja.Cell(fila, 1).GetString())];
    }

    private record AlertaDeChofer(int ChoferId, string Apellido, string Nombre);

    private record AlertaDeFlota(int VehiculoId, string Patente);

    private record FilaDeVencimientoDeFactura(int Id, string NumeroComprobante, string Cliente);
}

/// <summary>Semillas únicas, para no chocar contra DNI ni CUIL repetidos entre tests.</summary>
internal static class DatosDePruebaViajesReportes
{
    private static int _semilla = 91_000_000;

    public static int Semilla() => Interlocked.Increment(ref _semilla);
}
