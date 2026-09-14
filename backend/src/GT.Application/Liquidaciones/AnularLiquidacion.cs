using GT.Domain.Liquidaciones;

namespace GT.Application.Liquidaciones;

/// <summary>
/// Anula una liquidación <c>pendiente</c> sin pagos, con motivo, y libera sus viajes (FR-053 a FR-060).
///
/// <b>La confirmación vive en el backend</b> porque <c>anulada</c> es final (convención [005]): sin
/// <c>confirmado: true</c> responde <c>409</c> sin cambiar nada. Se pide <b>después</b> de verificar que la
/// anulación procede: pedir confirmación para algo que igual se va a rechazar es pedir una decisión que no
/// existe (data-model §Anular).
/// </summary>
public class AnularLiquidacion(
    IRepositorioLiquidaciones liquidaciones,
    ConsultarDetalleLiquidacion detalles,
    TimeProvider reloj)
{
    public const int LargoMaximoDelMotivo = 500;

    public async Task<ResultadoLiquidacion> EjecutarAsync(
        int id,
        AnulacionRequest? peticion,
        int usuarioId,
        CancellationToken cancelacion = default)
    {
        var liquidacion = await liquidaciones.ObtenerDetalleAsync(id, cancelacion);

        if (liquidacion is null)
        {
            return ResultadoLiquidacion.Rechazo(ErrorLiquidacion.NoEncontrada, MensajesLiquidaciones.NoEncontrada);
        }

        if (RechazoSiNoAnulable(liquidacion) is { } noAnulable)
        {
            return noAnulable;
        }

        var motivo = peticion?.Motivo?.Trim();

        if (string.IsNullOrEmpty(motivo))
        {
            return ResultadoLiquidacion.Rechazo(
                ErrorLiquidacion.MotivoRequerido,
                MensajesLiquidaciones.MotivoRequerido,
                "motivo");
        }

        if (motivo.Length > LargoMaximoDelMotivo)
        {
            return ResultadoLiquidacion.Rechazo(
                ErrorLiquidacion.DatosInvalidos,
                MensajesLiquidaciones.MotivoDemasiadoLargo,
                "motivo");
        }

        if (peticion!.Confirmado != true)
        {
            return ResultadoLiquidacion.Rechazo(
                ErrorLiquidacion.AnulacionRequiereConfirmacion,
                MensajesLiquidaciones.AnulacionRequiereConfirmacion(
                    liquidacion.Viajes.Count(vinculo => vinculo.Vigente)));
        }

        var aplicada = await liquidaciones.AnularAsync(
            id,
            motivo,
            usuarioId,
            reloj.GetUtcNow().UtcDateTime,
            cancelacion);

        if (!aplicada)
        {
            // Un pago se adelantó: se relee para decir el motivo actual.
            var actual = await liquidaciones.ObtenerDetalleAsync(id, cancelacion);

            return actual is null
                ? ResultadoLiquidacion.Rechazo(ErrorLiquidacion.NoEncontrada, MensajesLiquidaciones.NoEncontrada)
                : RechazoSiNoAnulable(actual)
                    ?? ResultadoLiquidacion.Rechazo(ErrorLiquidacion.DatosInvalidos, MensajesLiquidaciones.DatosInvalidos);
        }

        return ResultadoLiquidacion.Exito((await detalles.EjecutarAsync(id, cancelacion))!);
    }

    /// <summary>Con órdenes de pago, dice cuántas y por cuánto (FR-054, convención [004]).</summary>
    private static ResultadoLiquidacion? RechazoSiNoAnulable(Liquidacion liquidacion)
    {
        if (ReglasDeLiquidacion.MotivoNoAnulable(liquidacion.Estado, liquidacion.ImportePagado) is not { } motivo)
        {
            return null;
        }

        var conOrdenes = motivo is MotivoDeBloqueoLiquidacion.ConOrdenesDePago;

        return new ResultadoLiquidacion(
            ErrorLiquidacion.LiquidacionNoAnulable,
            Mensaje: MensajesLiquidaciones.NoAnulable(
                NumerosVisibles.Liquidacion(liquidacion.Numero),
                motivo,
                liquidacion.OrdenesDePago.Count,
                liquidacion.ImportePagado),
            Motivo: motivo,
            CantidadOrdenesDePago: conOrdenes ? liquidacion.OrdenesDePago.Count : null,
            ImportePagado: conOrdenes ? liquidacion.ImportePagado : null);
    }
}
