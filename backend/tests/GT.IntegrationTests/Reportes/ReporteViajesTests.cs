using System.Net;
using System.Net.Http.Json;
using ClosedXML.Excel;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;

namespace GT.IntegrationTests.Reportes;

/// <summary>
/// Paridad entre el reporte de viajes y el listado que alimenta la pantalla (US1, SC-002, FR-007).
///
/// <b>Lo que se verifica no es que el reporte traiga filas, sino que traiga las mismas</b>: la fuente
/// llama a la misma consulta del listado, así que el orden no se replica sino que se hereda. Si
/// alguien tocara un filtro de un solo lado, esto se cae.
/// </summary>
public class ReporteViajesTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    /// <summary>
    /// **Más de una página de viajes**: el archivo trae todas las filas del filtro, no las veinte
    /// visibles (FR-007).
    /// </summary>
    [Fact]
    public async Task ElReporteTraeTodasLasFilasDelFiltro_NoLasDeLaPagina()
    {
        var cliente = await app.CrearClienteAsync();
        var administrador = await app.CrearClienteAutenticadoAsync();

        for (var i = 0; i < 25; i++)
        {
            await app.CrearViajeAsync(cliente.Id, importe: 1000m * (i + 1));
        }

        var listado = await administrador.GetFromJsonAsync<PaginaDeViajes>(
            $"/api/viajes?clienteId={cliente.Id}");

        Assert.Equal(25, listado!.Total);
        Assert.Equal(20, listado.Items.Count);

        var filas = await FilasDelExcelAsync(administrador, $"/api/viajes/reporte?formato=excel&clienteId={cliente.Id}");

        Assert.Equal(25, filas.Count);
    }

    /// <summary>
    /// **El mismo orden de la pantalla**, fila por fila: fecha descendente, luego número descendente.
    /// </summary>
    [Fact]
    public async Task ElReporteTraeLasMismasFilasYEnElMismoOrdenQueElListado()
    {
        var cliente = await app.CrearClienteAsync();
        var administrador = await app.CrearClienteAutenticadoAsync();

        await app.CrearViajeAsync(cliente.Id, fecha: new DateOnly(2026, 9, 10), importe: 1000m);
        await app.CrearViajeAsync(cliente.Id, fecha: new DateOnly(2026, 9, 18), importe: 2000m);
        await app.CrearViajeAsync(cliente.Id, fecha: new DateOnly(2026, 9, 14), importe: 3000m);

        var listado = await administrador.GetFromJsonAsync<PaginaDeViajes>(
            $"/api/viajes?clienteId={cliente.Id}");

        var filas = await FilasDelExcelAsync(administrador, $"/api/viajes/reporte?formato=excel&clienteId={cliente.Id}");

        Assert.Equal(
            [.. listado!.Items.Select(viaje => viaje.Numero)],
            [.. filas.Select(fila => (int)fila[0])]);
    }

    /// <summary>El filtro por estado también llega igual a los dos lados.</summary>
    [Fact]
    public async Task ElFiltroDeEstadoDaLoMismoEnElListadoYEnElReporte()
    {
        var cliente = await app.CrearClienteAsync();
        var administrador = await app.CrearClienteAutenticadoAsync();

        await app.CrearViajeAsync(cliente.Id, estado: EstadoViaje.Pendiente);
        await app.CrearViajeAsync(cliente.Id, estado: EstadoViaje.Rendido);
        await app.CrearViajeAsync(cliente.Id, estado: EstadoViaje.Rendido);

        var listado = await administrador.GetFromJsonAsync<PaginaDeViajes>(
            $"/api/viajes?clienteId={cliente.Id}&estado=rendido");

        var filas = await FilasDelExcelAsync(
            administrador, $"/api/viajes/reporte?formato=excel&clienteId={cliente.Id}&estado=rendido");

        Assert.Equal(2, listado!.Total);
        Assert.Equal(listado.Total, filas.Count);
    }

    // ── El tope de FR-016 ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Con el tope bajado a tres y **cuatro** filas sembradas, el <c>409</c> llega con sus dos números y
    /// el mensaje ya armado por el backend (FR-016, SC-008).
    ///
    /// El tope se baja por configuración de prueba, no sembrando 5.001 filas: para eso se inyecta
    /// <c>OpcionesDeReporte</c> (research §13).
    /// </summary>
    [Fact]
    public async Task ConMasFilasQueElTope_Responde409ConSusDosNumeros()
    {
        var cliente = await app.CrearClienteAsync();

        for (var i = 0; i < 4; i++)
        {
            await app.CrearViajeAsync(cliente.Id);
        }

        using var conTope = app.ConTopeDe(3);
        var administrador = await conTope.ClienteAdministradorAsync();

        var respuesta = await administrador.GetAsync(
            $"/api/viajes/reporte?formato=pdf&clienteId={cliente.Id}");

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorDeReporte>();

        Assert.Equal("tope_de_filas_superado", error!.Codigo);
        Assert.Equal(4, error.Filas);
        Assert.Equal(3, error.Tope);
        Assert.Equal(
            "El filtro dejó 4 filas y el tope de un reporte es 3. Acotá los filtros y volvé a intentar.",
            error.Mensaje);
    }

    /// <summary>
    /// **El borde**: con filas **iguales** al tope el reporte se entrega, porque FR-016 rechaza
    /// "más de".
    /// </summary>
    [Fact]
    public async Task ConFilasIgualesAlTope_ElReporteSeEntrega()
    {
        var cliente = await app.CrearClienteAsync();

        for (var i = 0; i < 3; i++)
        {
            await app.CrearViajeAsync(cliente.Id);
        }

        using var conTope = app.ConTopeDe(3);
        var administrador = await conTope.ClienteAdministradorAsync();

        var respuesta = await administrador.GetAsync(
            $"/api/viajes/reporte?formato=pdf&clienteId={cliente.Id}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/pdf", respuesta.Content.Headers.ContentType?.MediaType);
    }

    // ── Las cabeceras del 200 ───────────────────────────────────────────────────────────────────

    [Fact]
    public async Task ElPdfSeSirveInlineYConNosniff()
    {
        var cliente = await app.CrearClienteAsync();
        await app.CrearViajeAsync(cliente.Id);
        var administrador = await app.CrearClienteAutenticadoAsync();

        var respuesta = await administrador.GetAsync(
            $"/api/viajes/reporte?formato=pdf&clienteId={cliente.Id}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/pdf", respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Equal("inline", respuesta.Content.Headers.ContentDisposition?.DispositionType);
        Assert.StartsWith("viajes-", respuesta.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
        Assert.EndsWith(".pdf", respuesta.Content.Headers.ContentDisposition.FileName!.Trim('"'));
        Assert.Equal("nosniff", respuesta.Headers.GetValues("X-Content-Type-Options").Single());
    }

    [Fact]
    public async Task ElExcelSeSirveComoAdjunto()
    {
        var cliente = await app.CrearClienteAsync();
        await app.CrearViajeAsync(cliente.Id);
        var administrador = await app.CrearClienteAutenticadoAsync();

        var respuesta = await administrador.GetAsync(
            $"/api/viajes/reporte?formato=excel&clienteId={cliente.Id}");

        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            respuesta.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", respuesta.Content.Headers.ContentDisposition?.DispositionType);
        Assert.EndsWith(".xlsx", respuesta.Content.Headers.ContentDisposition!.FileName!.Trim('"'));
    }

    // ── Andamio ─────────────────────────────────────────────────────────────────────────────────

    /// <summary>Las filas de datos del <c>.xlsx</c>, salteando el encabezado de FR-006.</summary>
    internal static async Task<List<object[]>> FilasDelExcelAsync(HttpClient cliente, string ruta)
    {
        var respuesta = await cliente.GetAsync(ruta);
        respuesta.EnsureSuccessStatusCode();

        using var memoria = new MemoryStream(await respuesta.Content.ReadAsByteArrayAsync());
        using var libro = new XLWorkbook(memoria);
        var hoja = libro.Worksheets.First();

        var usadas = hoja.RangeUsed()!;
        var primeraDeDatos = usadas.FirstRow().RowNumber() + 6;
        var ultima = usadas.LastRow().RowNumber();
        var columnas = usadas.LastColumn().ColumnNumber();

        var filas = new List<object[]>();

        for (var fila = primeraDeDatos; fila <= ultima; fila++)
        {
            // La fila de totales tiene vacía la primera columna: no es una fila de datos.
            if (hoja.Cell(fila, 1).IsEmpty())
            {
                continue;
            }

            filas.Add([.. Enumerable.Range(1, columnas).Select(columna => Valor(hoja.Cell(fila, columna)))]);
        }

        return filas;
    }

    private static object Valor(IXLCell celda) => celda.DataType switch
    {
        XLDataType.Number => (int)celda.GetValue<double>(),
        XLDataType.DateTime => celda.GetValue<DateTime>(),
        XLDataType.Boolean => celda.GetValue<bool>(),
        _ => celda.GetString(),
    };
}

internal record PaginaDeViajes(List<ViajeDeListado> Items, int Total, int Pagina, int TamanioPagina);

internal record ViajeDeListado(int Id, int Numero, string Fecha, string Estado, decimal Importe);
