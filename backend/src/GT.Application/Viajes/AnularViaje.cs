using GT.Domain.Viajes;

namespace GT.Application.Viajes;

/// <param name="Motivo">Obligatorio, hasta 500 caracteres (FR-036).</param>
public record AnulacionRequest(string? Motivo);

/// <summary>
/// Anula un viaje que no se hizo (FR-036, FR-037).
///
/// Procede desde <c>pendiente</c> y desde <c>en curso</c>; <b>no</b> desde <c>rendido</c>, que es
/// terminal e inmutable (FR-033, US6 esc. 6).
///
/// <b>El motivo es obligatorio y se escribe en la misma operación que el estado</b>: un viaje anulado
/// sin motivo sería un dato roto, así que no puede existir ni por un instante.
///
/// El chofer y el vehículo <b>quedan libres por el solo cambio de estado</b> —los índices filtrados
/// dejan de alcanzar al viaje—, y la asignación se conserva para saber a quién se le había encargado
/// (FR-037).
///
/// <c>anulado</c> es terminal: no hay ninguna acción que devuelva el viaje a <c>pendiente</c> ni a
/// <c>en curso</c>, y su importe no figura en ningún total (FR-047, US6 esc. 7).
/// </summary>
public class AnularViaje(IRepositorioViajes viajes, TimeProvider reloj)
{
    public async Task<ResultadoViaje> EjecutarAsync(
        int id,
        AnulacionRequest? peticion,
        int usuarioId,
        CancellationToken cancelacion = default)
    {
        var viaje = await viajes.ObtenerParaModificarAsync(id, cancelacion);

        if (viaje is null)
        {
            return new ResultadoViaje(ErrorViaje.NoEncontrado);
        }

        if (EstadoTerminal.Rechazo(viaje) is { } terminal)
        {
            return terminal;
        }

        if (!TransicionesDeViaje.EstaPermitida(viaje.Estado, EstadoViaje.Anulado))
        {
            return new ResultadoViaje(
                ErrorViaje.TransicionNoPermitida,
                NumeroDelViaje: viaje.Numero,
                EstadoActual: NombresDeEstadoViaje.EnTexto(viaje.Estado),
                EstadoPedido: NombresDeEstadoViaje.EnTexto(EstadoViaje.Anulado));
        }

        var motivo = peticion?.Motivo?.Trim();

        if (string.IsNullOrWhiteSpace(motivo) || motivo.Length > 500)
        {
            return new ResultadoViaje(
                ErrorViaje.MotivoRequerido,
                Campo: "motivo",
                NumeroDelViaje: viaje.Numero);
        }

        // El motivo se escribe antes del cambio de estado, y los dos se guardan juntos: el registro
        // del cambio termina en un solo `SaveChanges`.
        viaje.MotivoAnulacion = motivo;

        await viajes.RegistrarCambioDeEstadoAsync(
            viaje,
            EstadoViaje.Anulado,
            usuarioId,
            reloj.GetUtcNow().UtcDateTime,
            cancelacion);

        var ficha = await viajes.ObtenerFichaAsync(id, cancelacion);

        return new ResultadoViaje(
            ErrorViaje.Ninguno,
            ViajeDetalle.Desde(ficha!, MomentoDeLectura.Desde(reloj)),
            NumeroDelViaje: viaje.Numero);
    }
}
