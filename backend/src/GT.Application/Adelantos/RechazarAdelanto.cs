using GT.Domain.Adelantos;

namespace GT.Application.Adelantos;

/// <summary>
/// Rechaza un adelanto <c>pendiente</c> con motivo (FR-021, FR-024 a FR-026).
///
/// <b>La confirmación vive en el backend</b> porque <c>rechazado</c> es final (convención [005]): sin
/// <c>confirmado: true</c> responde <c>409</c> sin cambiar nada. Se pide <b>después</b> de verificar que el
/// rechazo procede: pedir confirmación para algo que igual se va a rechazar es pedir una decisión que no
/// existe (research §4).
/// </summary>
public class RechazarAdelanto(
    IRepositorioAdelantos adelantos,
    ConsultarDetalleAdelanto detalles,
    TimeProvider reloj)
{
    public const int LargoMaximoDelMotivo = 500;

    public async Task<ResultadoAdelanto> EjecutarAsync(
        int id,
        RechazoRequest? peticion,
        int usuarioId,
        CancellationToken cancelacion = default)
    {
        var adelanto = await adelantos.ObtenerDetalleAsync(id, cancelacion);

        if (adelanto is null)
        {
            return ResultadoAdelanto.NoEncontrado();
        }

        if (ReglasDeAdelanto.MotivoNoResoluble(adelanto.Estado) is { } estado)
        {
            return ResultadoAdelanto.NoResoluble(estado);
        }

        var motivo = peticion?.Motivo?.Trim();

        if (string.IsNullOrEmpty(motivo))
        {
            return ResultadoAdelanto.Rechazo(
                ErrorAdelanto.MotivoRequerido,
                MensajesAdelantos.MotivoDelRechazoRequerido,
                "motivo");
        }

        if (motivo.Length > LargoMaximoDelMotivo)
        {
            return ResultadoAdelanto.Rechazo(
                ErrorAdelanto.DatosInvalidos,
                MensajesAdelantos.MotivoDeCierreDemasiadoLargo,
                "motivo");
        }

        if (peticion!.Confirmado != true)
        {
            return ResultadoAdelanto.Rechazo(
                ErrorAdelanto.RechazoRequiereConfirmacion,
                MensajesAdelantos.RechazoRequiereConfirmacion);
        }

        if (!await adelantos.RechazarAsync(id, motivo, usuarioId, reloj.GetUtcNow().UtcDateTime, cancelacion))
        {
            return await RelecturaTrasCarrera.NoResolubleAsync(adelantos, id, cancelacion);
        }

        return ResultadoAdelanto.Exito((await detalles.EjecutarAsync(id, cancelacion))!);
    }
}
