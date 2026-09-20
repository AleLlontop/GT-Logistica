using GT.Application.Choferes;

namespace GT.Application.Caja;

/// <summary>
/// Movimientos por rango de fechas y/o por caja (FR-023 a FR-025). Todo lo que decide qué filas salen vive en
/// la consulta del repositorio; acá se rechaza el rango invertido <b>sin consultar</b> y se convierten los
/// días a instantes.
///
/// <b>Los días son de Argentina</b> (tasks.md decisión 4): <c>desde</c> empieza a las 00:00 −03:00 y
/// <c>hasta</c> termina antes de las 00:00 −03:00 del día siguiente. Un movimiento del 16/09 a las 23:30 de
/// Argentina —17/09 02:30 UTC— entra con <c>hasta</c> = 16/09. Desplazamiento fijo, como
/// <c>FechaHoyArgentina</c>: Argentina no tiene horario de verano.
/// </summary>
public class ConsultarMovimientos(IRepositorioCaja cajas)
{
    private static readonly TimeSpan DesplazamientoArgentina = TimeSpan.FromHours(-3);

    /// <param name="tamanioPagina">
    /// <c>null</c> es el tamaño de siempre y <b>ninguna llamada existente cambia</b>. El reporte del
    /// Módulo 12 lo usa para traerse todas las filas del filtro llamando a <b>esta misma consulta</b>,
    /// con su mismo orden y su mismo corte por día de Argentina (research §3).
    /// </param>
    public async Task<ResultadoConsultaMovimientos> EjecutarAsync(
        FiltrosDeMovimientos filtros,
        int? tamanioPagina = null,
        CancellationToken cancelacion = default)
    {
        if (filtros is { Desde: { } desde, Hasta: { } hasta } && desde > hasta)
        {
            return new ResultadoConsultaMovimientos(
                null,
                ResultadoCaja.Rechazo(ErrorCaja.RangoInvalido, MensajesCaja.RangoInvalido, "desde"));
        }

        var pagina = await cajas.ConsultarMovimientosAsync(
            filtros.Desde is { } inicio ? InicioDelDia(inicio) : null,
            filtros.Hasta is { } fin ? InicioDelDia(fin.AddDays(1)) : null,
            filtros.CajaId,
            Math.Max(filtros.Pagina, 1),
            tamanioPagina,
            cancelacion);

        return new ResultadoConsultaMovimientos(pagina, null);
    }

    /// <summary>Las 00:00 de ese día en Argentina, como instante UTC.</summary>
    public static DateTime InicioDelDia(DateOnly dia) =>
        new DateTimeOffset(dia.ToDateTime(TimeOnly.MinValue), DesplazamientoArgentina).UtcDateTime;
}

/// <summary>Los movimientos de una caja, para su pantalla (CA5). <c>null</c> si la caja no existe.</summary>
public class ConsultarMovimientosDeCaja(IRepositorioCaja cajas)
{
    public async Task<PaginaDe<MovimientoListado>?> EjecutarAsync(
        int cajaId,
        int pagina,
        int usuarioId,
        CancellationToken cancelacion = default)
    {
        if (await cajas.MotivoNoOperableAsync(cajaId, usuarioId, cancelacion) is MotivoNoOperable.NoEncontrada)
        {
            return null;
        }

        return await cajas.ConsultarMovimientosDeCajaAsync(cajaId, Math.Max(pagina, 1), cancelacion);
    }
}
