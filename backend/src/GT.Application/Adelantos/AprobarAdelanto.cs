using GT.Domain.Adelantos;

namespace GT.Application.Adelantos;

/// <summary>
/// Aprueba un adelanto <c>pendiente</c> sin pedir nada más (FR-021 a FR-023, FR-026).
///
/// <b>Sin confirmación</b>: se deshace con la anulación, que sí la pide (research §4). Quien lo registró
/// puede aprobarlo (FR-022), y no se vuelve a mirar si la persona sigue activa: esa regla es del registro.
/// </summary>
public class AprobarAdelanto(
    IRepositorioAdelantos adelantos,
    ConsultarDetalleAdelanto detalles,
    TimeProvider reloj)
{
    public async Task<ResultadoAdelanto> EjecutarAsync(int id, int usuarioId, CancellationToken cancelacion = default)
    {
        var adelanto = await adelantos.ObtenerDetalleAsync(id, cancelacion);

        if (adelanto is null)
        {
            return ResultadoAdelanto.NoEncontrado();
        }

        // La consulta previa da el mensaje bueno; el `UPDATE` condicional cierra la carrera.
        if (ReglasDeAdelanto.MotivoNoResoluble(adelanto.Estado) is { } estado)
        {
            return ResultadoAdelanto.NoResoluble(estado);
        }

        if (!await adelantos.AprobarAsync(id, usuarioId, reloj.GetUtcNow().UtcDateTime, cancelacion))
        {
            return await RelecturaTrasCarrera.NoResolubleAsync(adelantos, id, cancelacion);
        }

        return ResultadoAdelanto.Exito((await detalles.EjecutarAsync(id, cancelacion))!);
    }
}

/// <summary>
/// Qué responder cuando el <c>UPDATE</c> condicional no afectó ninguna fila: otra operación se adelantó.
/// <b>Se relee</b> para decir el estado en que quedó (FR-026, FR-032, convención [009]).
/// </summary>
internal static class RelecturaTrasCarrera
{
    public static async Task<ResultadoAdelanto> NoResolubleAsync(
        IRepositorioAdelantos adelantos,
        int id,
        CancellationToken cancelacion)
    {
        var actual = await adelantos.ObtenerDetalleAsync(id, cancelacion);

        return actual is null ? ResultadoAdelanto.NoEncontrado() : ResultadoAdelanto.NoResoluble(actual.Estado);
    }

    public static async Task<ResultadoAdelanto> NoAnulableAsync(
        IRepositorioAdelantos adelantos,
        int id,
        CancellationToken cancelacion)
    {
        var actual = await adelantos.ObtenerDetalleAsync(id, cancelacion);

        return actual is null ? ResultadoAdelanto.NoEncontrado() : ResultadoAdelanto.NoAnulable(actual.Estado);
    }
}
