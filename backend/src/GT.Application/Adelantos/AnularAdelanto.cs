using GT.Domain.Adelantos;

namespace GT.Application.Adelantos;

/// <summary>
/// Anula un adelanto <c>aprobado</c> con motivo, sin borrarlo (FR-027 a FR-032).
///
/// Mismo orden que el rechazo —existencia, estado, motivo, confirmación— y la misma confirmación en el
/// backend, porque <c>anulado</c> es final y revierte algo que ya contaba como adelantado (research §4).
/// </summary>
public class AnularAdelanto(
    IRepositorioAdelantos adelantos,
    ConsultarDetalleAdelanto detalles,
    TimeProvider reloj)
{
    public const int LargoMaximoDelMotivo = 500;

    public async Task<ResultadoAdelanto> EjecutarAsync(
        int id,
        AnulacionAdelantoRequest? peticion,
        int usuarioId,
        CancellationToken cancelacion = default)
    {
        var adelanto = await adelantos.ObtenerDetalleAsync(id, cancelacion);

        if (adelanto is null)
        {
            return ResultadoAdelanto.NoEncontrado();
        }

        if (ReglasDeAdelanto.MotivoNoAnulable(adelanto.Estado) is { } estado)
        {
            return ResultadoAdelanto.NoAnulable(estado);
        }

        var motivo = peticion?.Motivo?.Trim();

        if (string.IsNullOrEmpty(motivo))
        {
            return ResultadoAdelanto.Rechazo(
                ErrorAdelanto.MotivoRequerido,
                MensajesAdelantos.MotivoDeLaAnulacionRequerido,
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
            var persona = adelanto.Persona!;

            return ResultadoAdelanto.Rechazo(
                ErrorAdelanto.AnulacionRequiereConfirmacion,
                MensajesAdelantos.AnulacionRequiereConfirmacion(adelanto.Importe, persona.NombreCompleto));
        }

        if (!await adelantos.AnularAsync(id, motivo, usuarioId, reloj.GetUtcNow().UtcDateTime, cancelacion))
        {
            return await RelecturaTrasCarrera.NoAnulableAsync(adelantos, id, cancelacion);
        }

        return ResultadoAdelanto.Exito((await detalles.EjecutarAsync(id, cancelacion))!);
    }
}
