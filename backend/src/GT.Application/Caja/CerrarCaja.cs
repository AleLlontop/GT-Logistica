namespace GT.Application.Caja;

/// <summary>
/// Cierra la caja con el saldo final que la pantalla mostró (FR-017 a FR-022).
///
/// <b>La confirmación viaja con el número que confirma</b> (research §3): sin <c>confirmado</c> o sin
/// <c>saldoFinalConfirmado</c> no se toca nada y vuelve el resumen actual; con un saldo distinto del recién
/// calculado —un movimiento entró entre medio— tampoco, y vuelve el resumen nuevo para confirmar de nuevo.
/// </summary>
public class CerrarCaja(IRepositorioCaja cajas, ConsultarDetalleCaja detalles, TimeProvider reloj)
{
    public async Task<ResultadoCaja> EjecutarAsync(
        int cajaId,
        CierreRequest? peticion,
        int usuarioId,
        CancellationToken cancelacion = default)
    {
        if (peticion is not { Confirmado: true, SaldoFinalConfirmado: { } saldoFinalConfirmado })
        {
            if (await cajas.MotivoNoOperableAsync(cajaId, usuarioId, cancelacion) is { } motivo)
            {
                return ResultadoCaja.NoOperable(motivo);
            }

            return ResultadoCaja.ConResumen(
                ErrorCaja.ConfirmacionRequerida,
                MensajesCaja.ConfirmacionRequerida,
                await cajas.ConsultarResumenAsync(cajaId, cancelacion));
        }

        var cierre = await cajas.CerrarAsync(
            cajaId,
            usuarioId,
            saldoFinalConfirmado,
            reloj.GetUtcNow().UtcDateTime,
            cancelacion);

        if (cierre.NoOperable is { } noOperable)
        {
            return ResultadoCaja.NoOperable(noOperable);
        }

        if (cierre.ResumenActual is { } resumen)
        {
            return ResultadoCaja.ConResumen(
                ErrorCaja.CierreDesactualizado,
                MensajesCaja.CierreDesactualizado,
                resumen);
        }

        return ResultadoCaja.Exito((await detalles.EjecutarAsync(cajaId, usuarioId, cancelacion))!);
    }
}
