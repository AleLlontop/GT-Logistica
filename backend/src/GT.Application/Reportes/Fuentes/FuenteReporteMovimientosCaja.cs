using GT.Application.Caja;
using GT.Domain.Caja;

namespace GT.Application.Reportes.Fuentes;

/// <summary>Los filtros de la pantalla de movimientos, con los mismos nombres que el listado.</summary>
public record FiltrosDelReporteDeCaja(
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    int? CajaId = null);

/// <summary>
/// El reporte de movimientos de caja (US3, data-model §3.5).
///
/// <b>Llama a la misma consulta del listado</b>, con el tamaño de página puesto en el tope. Con eso
/// hereda las dos cosas que SC-002 exige y que replicar rompería en silencio: el orden —fecha
/// descendente, luego <c>Id</c>— y el <b>corte por día de Argentina</b> del rango de fechas, que
/// <see cref="ConsultarMovimientos"/> resuelve convirtiendo cada día a su instante UTC (convención
/// [011]). Acá no se vuelve a escribir esa regla.
///
/// Los <b>tres</b> totales de FR-009 se calculan sobre las filas del propio reporte, con
/// <c>Neto = ingresos − egresos</c>: es lo que hace que SC-004 se compruebe sumando las filas del
/// archivo.
/// </summary>
public class FuenteReporteMovimientosCaja(
    ConsultarMovimientos consulta,
    IRepositorioCaja cajas,
    ArmadoDeReporte armado)
{
    public const string Titulo = "Reporte de movimientos de caja";

    private static readonly IReadOnlyList<ColumnaDeReporte> Columnas =
    [
        ColumnaDeReporte.Fecha("Fecha"),
        ColumnaDeReporte.Texto("Tipo"),
        ColumnaDeReporte.Importe("Importe"),
        ColumnaDeReporte.Texto("Concepto"),
        ColumnaDeReporte.Texto("Responsable"),
        ColumnaDeReporte.Texto("Referencia"),
    ];

    public async Task<ResultadoReporte> EjecutarAsync(
        FiltrosDelReporteDeCaja filtros,
        FormatoDeReporte formato,
        string generadoPor,
        CancellationToken cancelacion = default)
    {
        var resultado = await consulta.EjecutarAsync(
            new FiltrosDeMovimientos(filtros.Desde, filtros.Hasta, filtros.CajaId, Pagina: 1),
            armado.TamanioParaTraerTodo,
            cancelacion);

        // El rango invertido lo rechaza la consulta del listado, sin consultar. Acá se traduce a un
        // reporte vacío en vez de inventarle un rechazo propio: la pantalla ya no consulta con el
        // rango invertido, y duplicar su mensaje serían dos textos que se separan.
        if (resultado.Pagina is not { } pagina)
        {
            return armado.Entregar(
                TipoDeReporte.MovimientosCaja,
                formato,
                generadoPor,
                Titulo,
                await LineaDeFiltrosAsync(filtros, cancelacion),
                Columnas,
                [],
                Totales([]));
        }

        if (armado.SuperaElTope(pagina.Total))
        {
            return armado.TopeSuperado(pagina.Total);
        }

        return armado.Entregar(
            TipoDeReporte.MovimientosCaja,
            formato,
            generadoPor,
            Titulo,
            await LineaDeFiltrosAsync(filtros, cancelacion),
            Columnas,
            [.. pagina.Items.Select(Fila)],
            Totales(pagina.Items));
    }

    private static IReadOnlyList<CeldaDeReporte> Fila(MovimientoListado movimiento) =>
    [
        CeldaDeReporte.Fecha(DateOnly.FromDateTime(EncabezadoDeReporte.EnArgentina(movimiento.Fecha))),
        // **La palabra de la pantalla**: `Ingreso`/`Egreso`, no el `ingreso`/`egreso` del JSON
        // (FR-010, data-model §2.5).
        CeldaDeReporte.Texto(NombresDeEstadoCaja.EnPantalla(
            NombresDeEstadoCaja.LeerTipo(movimiento.Tipo)
                ?? throw new InvalidOperationException(
                    $"El movimiento {movimiento.Id} llegó con un tipo desconocido: {movimiento.Tipo}."))),
        CeldaDeReporte.Importe(movimiento.Importe),
        CeldaDeReporte.Texto(movimiento.Concepto),
        CeldaDeReporte.Texto(movimiento.Responsable.Nombre),
        // Sin referencia, la celda queda vacía.
        CeldaDeReporte.Texto(movimiento.Referencia),
    ];

    /// <summary>Los tres de FR-009, sobre las filas del reporte.</summary>
    private static IReadOnlyList<TotalDeReporte> Totales(IReadOnlyList<MovimientoListado> movimientos)
    {
        var ingresos = movimientos
            .Where(movimiento => NombresDeEstadoCaja.LeerTipo(movimiento.Tipo) == TipoMovimientoCaja.Ingreso)
            .Sum(movimiento => movimiento.Importe);

        var egresos = movimientos
            .Where(movimiento => NombresDeEstadoCaja.LeerTipo(movimiento.Tipo) == TipoMovimientoCaja.Egreso)
            .Sum(movimiento => movimiento.Importe);

        return
        [
            new TotalDeReporte("Total de ingresos", ingresos),
            new TotalDeReporte("Total de egresos", egresos),
            new TotalDeReporte("Neto", ingresos - egresos),
        ];
    }

    /// <summary>
    /// <c>Desde · Hasta · Caja</c>, con la caja resuelta <b>a su nombre visible en el backend</b>
    /// (research §9): el mismo par responsable + día de apertura con el que el desplegable la nombra,
    /// porque una caja no tiene número visible.
    /// </summary>
    private async Task<string> LineaDeFiltrosAsync(
        FiltrosDelReporteDeCaja filtros,
        CancellationToken cancelacion)
    {
        string? caja = null;

        if (filtros.CajaId is { } cajaId)
        {
            // `usuarioId: 0` porque acá sólo se lee cómo se llama la caja: ese parámetro únicamente
            // decide `PuedeOperar`, que este reporte no usa.
            var detalle = await cajas.ObtenerDetalleAsync(cajaId, usuarioId: 0, cancelacion);

            caja = detalle is null
                ? null
                : $"{detalle.Responsable.Nombre} · " +
                  $"{EncabezadoDeReporte.EnArgentina(detalle.FechaApertura):dd/MM/yyyy}";
        }

        return new LineaDeFiltros()
            .Con("Desde", filtros.Desde)
            .Con("Hasta", filtros.Hasta)
            .Con("Caja", caja)
            .ToString();
    }
}
