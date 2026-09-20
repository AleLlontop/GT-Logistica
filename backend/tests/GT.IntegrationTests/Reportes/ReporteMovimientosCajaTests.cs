using System.Net.Http.Json;
using GT.Domain.Caja;
using GT.IntegrationTests.Caja;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Reportes;

/// <summary>
/// Paridad del reporte de movimientos de caja con su listado (US3, SC-002), y el corte del rango de
/// fechas <b>en el día de Argentina</b>.
///
/// El reporte llama a la misma consulta del listado, así que hereda el orden y el corte por día sin
/// volver a escribir esa regla (convención [011], research §3). El test de los bordes es lo que prueba
/// que de verdad la hereda: un movimiento de las 23:30 de Argentina es de las 02:30 UTC del día
/// siguiente, y comparar el día UTC lo dejaría afuera.
/// </summary>
public class ReporteMovimientosCajaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task ElReporteTraeLosMismosMovimientosYEnElMismoOrden()
    {
        var (usuario, cliente) = await app.EmpleadoAsync();
        var caja = await app.CrearCajaAsync(usuario.Id);

        await app.CrearMovimientoAsync(caja.Id, usuario.Id, TipoMovimientoCaja.Ingreso, 15_000m);
        await app.CrearMovimientoAsync(caja.Id, usuario.Id, TipoMovimientoCaja.Egreso, 4_000m);
        await app.CrearMovimientoAsync(caja.Id, usuario.Id, TipoMovimientoCaja.Ingreso, 7_500m);

        var listado = await cliente.GetFromJsonAsync<PaginaDeMovimientos>(
            $"/api/movimientos-caja?cajaId={caja.Id}");

        var administrador = await app.CrearClienteAutenticadoAsync();

        var filas = await ReporteViajesTests.FilasDelExcelAsync(
            administrador, $"/api/movimientos-caja/reporte?formato=excel&cajaId={caja.Id}");

        Assert.Equal(3, listado!.Total);
        Assert.Equal(listado.Total, filas.Count);

        // Mismo orden: fecha descendente, luego Id descendente.
        Assert.Equal(
            [.. listado.Items.Select(movimiento => movimiento.Importe)],
            [.. filas.Select(fila => Convert.ToDecimal(fila[2]))]);
    }

    /// <summary>
    /// **El tipo va con la palabra de la pantalla**: <c>Ingreso</c> y <c>Egreso</c>, nunca el
    /// <c>ingreso</c>/<c>egreso</c> del JSON (FR-010).
    /// </summary>
    [Fact]
    public async Task ElTipoVaConLaPalabraDeLaPantalla()
    {
        var (usuario, _) = await app.EmpleadoAsync();
        var caja = await app.CrearCajaAsync(usuario.Id);

        await app.CrearMovimientoAsync(caja.Id, usuario.Id, TipoMovimientoCaja.Ingreso);
        await app.CrearMovimientoAsync(caja.Id, usuario.Id, TipoMovimientoCaja.Egreso);

        var administrador = await app.CrearClienteAutenticadoAsync();

        var filas = await ReporteViajesTests.FilasDelExcelAsync(
            administrador, $"/api/movimientos-caja/reporte?formato=excel&cajaId={caja.Id}");

        Assert.Equal(["Egreso", "Ingreso"], [.. filas.Select(fila => (string)fila[1]).Order()]);
    }

    // ── El corte por día de Argentina (convención [011]) ────────────────────────────────────────

    /// <summary>
    /// Los cuatro bordes: <b>00:30 y 23:30 del día de Argentina entran; 23:59 del anterior y 00:00 del
    /// siguiente quedan afuera.</b>
    ///
    /// El reporte no repite la regla: la hereda de la consulta del listado. Si alguien la tocara de un
    /// solo lado, esto se cae.
    /// </summary>
    [Fact]
    public async Task ElRangoCortaEnElDiaDeArgentina()
    {
        var (usuario, _) = await app.EmpleadoAsync();
        var caja = await app.CrearCajaAsync(usuario.Id);

        var dia = new DateOnly(2026, 9, 16);

        // Instantes UTC de cada borde, en hora de Argentina (UTC−3).
        await app.CrearMovimientoAsync(caja.Id, usuario.Id, fecha: EnArgentina(dia, 0, 30), importe: 1m);
        await app.CrearMovimientoAsync(caja.Id, usuario.Id, fecha: EnArgentina(dia, 23, 30), importe: 2m);
        await app.CrearMovimientoAsync(
            caja.Id, usuario.Id, fecha: EnArgentina(dia.AddDays(-1), 23, 59), importe: 3m);
        await app.CrearMovimientoAsync(
            caja.Id, usuario.Id, fecha: EnArgentina(dia.AddDays(1), 0, 0), importe: 4m);

        var administrador = await app.CrearClienteAutenticadoAsync();

        var filas = await ReporteViajesTests.FilasDelExcelAsync(
            administrador,
            $"/api/movimientos-caja/reporte?formato=excel&cajaId={caja.Id}&desde=2026-09-16&hasta=2026-09-16");

        var importes = filas.Select(fila => Convert.ToDecimal(fila[2])).Order().ToList();

        Assert.Equal([1m, 2m], importes);
    }

    // ── Los tres totales (FR-009, SC-004) ───────────────────────────────────────────────────────

    /// <summary>
    /// **Los tres totales cierran contra las filas del propio archivo**, que es como SC-004 se
    /// comprueba con la calculadora de la planilla.
    /// </summary>
    [Fact]
    public async Task LosTresTotalesCierranContraLasFilasDelArchivo()
    {
        var (usuario, _) = await app.EmpleadoAsync();
        var caja = await app.CrearCajaAsync(usuario.Id);

        await app.CrearMovimientoAsync(caja.Id, usuario.Id, TipoMovimientoCaja.Ingreso, 150_000m);
        await app.CrearMovimientoAsync(caja.Id, usuario.Id, TipoMovimientoCaja.Ingreso, 34_500m);
        await app.CrearMovimientoAsync(caja.Id, usuario.Id, TipoMovimientoCaja.Egreso, 22_000m);

        var administrador = await app.CrearClienteAutenticadoAsync();
        var ruta = $"/api/movimientos-caja/reporte?formato=excel&cajaId={caja.Id}";

        var filas = await ReporteViajesTests.FilasDelExcelAsync(administrador, ruta);
        var totales = await TotalesDelExcelAsync(administrador, ruta);

        var ingresos = filas
            .Where(fila => (string)fila[1] == "Ingreso")
            .Sum(fila => Convert.ToDecimal(fila[2]));

        var egresos = filas
            .Where(fila => (string)fila[1] == "Egreso")
            .Sum(fila => Convert.ToDecimal(fila[2]));

        Assert.Equal(3, totales.Count);
        Assert.Equal(("Total de ingresos", ingresos), totales[0]);
        Assert.Equal(("Total de egresos", egresos), totales[1]);
        Assert.Equal(("Neto", ingresos - egresos), totales[2]);
    }

    /// <summary>La caja del filtro se nombra como la nombra el desplegable de la pantalla.</summary>
    [Fact]
    public async Task LaLineaDeFiltrosNombraLaCaja()
    {
        var (usuario, _) = await app.EmpleadoAsync();
        var caja = await app.CrearCajaAsync(usuario.Id);
        await app.CrearMovimientoAsync(caja.Id, usuario.Id);

        var administrador = await app.CrearClienteAutenticadoAsync();

        var encabezado = await ReportesDeVencimientosTests.EncabezadoDelExcelAsync(
            administrador, $"/api/movimientos-caja/reporte?formato=excel&cajaId={caja.Id}");

        Assert.Equal("Reporte de movimientos de caja", encabezado[0]);
        Assert.StartsWith($"Caja: {usuario.Username} · ", encabezado[1]);
    }

    // ── Andamio ─────────────────────────────────────────────────────────────────────────────────

    private static DateTime EnArgentina(DateOnly dia, int hora, int minuto) =>
        new DateTimeOffset(
            dia.ToDateTime(new TimeOnly(hora, minuto)),
            TimeSpan.FromHours(-3)).UtcDateTime;

    /// <summary>Las filas del pie: etiqueta e importe, leídos como número.</summary>
    private static async Task<List<(string Etiqueta, decimal Importe)>> TotalesDelExcelAsync(
        HttpClient cliente,
        string ruta)
    {
        var respuesta = await cliente.GetAsync(ruta);
        respuesta.EnsureSuccessStatusCode();

        using var memoria = new MemoryStream(await respuesta.Content.ReadAsByteArrayAsync());
        using var libro = new ClosedXML.Excel.XLWorkbook(memoria);
        var hoja = libro.Worksheets.First();

        var usadas = hoja.RangeUsed()!;
        var ultimaColumna = usadas.LastColumn().ColumnNumber();

        var totales = new List<(string, decimal)>();

        for (var fila = usadas.FirstRow().RowNumber(); fila <= usadas.LastRow().RowNumber(); fila++)
        {
            var etiqueta = hoja.Cell(fila, ultimaColumna - 1).GetString();

            if (etiqueta is "Total de ingresos" or "Total de egresos" or "Neto")
            {
                totales.Add((etiqueta, hoja.Cell(fila, ultimaColumna).GetValue<decimal>()));
            }
        }

        return totales;
    }

    private record PaginaDeMovimientos(List<MovimientoLeido> Items, int Total);

    private record MovimientoLeido(int Id, string Tipo, decimal Importe);
}
