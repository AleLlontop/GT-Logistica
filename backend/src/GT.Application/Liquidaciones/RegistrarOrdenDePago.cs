using GT.Domain.Choferes;
using GT.Domain.Liquidaciones;

namespace GT.Application.Liquidaciones;

/// <summary>
/// Registra una orden de pago sobre una liquidación <c>pendiente</c>; si deja el saldo en cero, la
/// liquidación pasa a <c>pagada</c> en la misma operación (FR-036 a FR-044).
///
/// <b>La confirmación hace el viaje de ida y vuelta</b>: FR-040 pide mostrar lo que va a restar pagar, y
/// ese número lo calcula el servidor sobre el saldo actual. Sin <c>confirmado: true</c> responde
/// <c>409</c> con los importes y no registra nada (research §7).
/// </summary>
public class RegistrarOrdenDePago(
    IRepositorioLiquidaciones liquidaciones,
    ConsultarDetalleLiquidacion detalles,
    TimeProvider reloj)
{
    public async Task<ResultadoLiquidacion> EjecutarAsync(
        int id,
        OrdenDePagoRequest? peticion,
        int usuarioId,
        CancellationToken cancelacion = default)
    {
        var liquidacion = await liquidaciones.ObtenerDetalleAsync(id, cancelacion);

        if (liquidacion is null)
        {
            return ResultadoLiquidacion.Rechazo(ErrorLiquidacion.NoEncontrada, MensajesLiquidaciones.NoEncontrada);
        }

        if (liquidacion.Estado is not EstadoLiquidacion.Pendiente)
        {
            return NoPagable(liquidacion);
        }

        // `hoy` y el instante de la orden salen del mismo reloj, en el mismo caso de uso (data-model §Pagar).
        var ahora = reloj.GetUtcNow();
        var hoy = FechaHoyArgentina.Desde(ahora);
        var desde = ConsultarDetalleLiquidacion.FechaDeGeneracion(liquidacion);

        if (peticion?.FechaPago is not { } fechaPago)
        {
            return ResultadoLiquidacion.Rechazo(
                ErrorLiquidacion.DatosInvalidos,
                MensajesLiquidaciones.FechaDePagoRequerida,
                "fechaPago");
        }

        if (!ReglasDeLiquidacion.FechaDePagoValida(fechaPago, desde, hoy))
        {
            return new ResultadoLiquidacion(
                ErrorLiquidacion.FechaDePagoFueraDeRango,
                Campo: "fechaPago",
                Mensaje: MensajesLiquidaciones.FechaDePagoFueraDeRango(desde),
                Desde: desde,
                Hasta: hoy);
        }

        // Más de dos decimales no es un importe en pesos: la columna los redondearía en silencio.
        if (peticion.Importe is not { } importe || importe <= 0m || decimal.Round(importe, 2) != importe)
        {
            return ResultadoLiquidacion.Rechazo(
                ErrorLiquidacion.DatosInvalidos,
                MensajesLiquidaciones.ImporteInvalido,
                "importe");
        }

        var resta = ReglasDeLiquidacion.RestaPagar(liquidacion.ImporteTotal, liquidacion.ImportePagado);

        if (importe > resta)
        {
            return new ResultadoLiquidacion(
                ErrorLiquidacion.ImporteSuperaSaldo,
                Campo: "importe",
                Mensaje: MensajesLiquidaciones.ImporteSuperaSaldo(resta),
                RestaPagar: resta);
        }

        if (peticion.Confirmado != true)
        {
            var quedaPagada = ReglasDeLiquidacion.EstadoTrasPago(
                liquidacion.ImporteTotal,
                liquidacion.ImportePagado,
                importe) is EstadoLiquidacion.Pagada;

            return new ResultadoLiquidacion(
                ErrorLiquidacion.PagoRequiereConfirmacion,
                Mensaje: MensajesLiquidaciones.PagoRequiereConfirmacion(importe, resta - importe, quedaPagada),
                Confirmacion: new ConfirmacionDePagoCalculada(importe, resta, resta - importe, quedaPagada));
        }

        var registrada = await liquidaciones.RegistrarPagoAsync(
            id,
            fechaPago,
            importe,
            usuarioId,
            ahora.UtcDateTime,
            cancelacion);

        if (registrada is null)
        {
            // El `UPDATE` no afectó ninguna fila: otro pago o una anulación se adelantaron. Se relee para
            // distinguir los dos rechazos (data-model §Pagar).
            var actual = await liquidaciones.ObtenerDetalleAsync(id, cancelacion);

            if (actual is null)
            {
                return ResultadoLiquidacion.Rechazo(ErrorLiquidacion.NoEncontrada, MensajesLiquidaciones.NoEncontrada);
            }

            if (actual.Estado is not EstadoLiquidacion.Pendiente)
            {
                return NoPagable(actual);
            }

            var restaActual = ReglasDeLiquidacion.RestaPagar(actual.ImporteTotal, actual.ImportePagado);

            return new ResultadoLiquidacion(
                ErrorLiquidacion.ImporteSuperaSaldo,
                Campo: "importe",
                Mensaje: MensajesLiquidaciones.ImporteSuperaSaldoPorOtroPago(restaActual),
                RestaPagar: restaActual);
        }

        return ResultadoLiquidacion.Exito((await detalles.EjecutarAsync(id, cancelacion))!);
    }

    private static ResultadoLiquidacion NoPagable(Liquidacion liquidacion) =>
        new(
            ErrorLiquidacion.LiquidacionNoPagable,
            Mensaje: MensajesLiquidaciones.NoPagable(
                NumerosVisibles.Liquidacion(liquidacion.Numero),
                liquidacion.Estado),
            Motivo: liquidacion.Estado is EstadoLiquidacion.Anulada
                ? MotivoDeBloqueoLiquidacion.Anulada
                : MotivoDeBloqueoLiquidacion.Pagada);
}
