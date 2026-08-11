using GT.Domain.Viajes;

namespace GT.Application.Viajes;

/// <param name="Confirmado">
/// Sólo hace falta cuando el importe es cero. Con importe mayor a cero se ignora: no hay nada que
/// confirmar.
/// </param>
public record RendicionRequest(bool? Confirmado);

/// <summary>
/// Pasa el viaje de <c>en curso</c> a <c>rendido</c> (FR-033, FR-037, FR-038).
///
/// <b>La confirmación de FR-038 vive acá, en el backend, a diferencia de todas las confirmaciones
/// anteriores del sistema.</b> Hasta el Módulo 4 la confirmación la pedía la pantalla y el endpoint
/// ejecutaba —dar de baja un vehículo, un tipo, un cliente—, porque todas esas se deshacen. Rendir
/// con importe en cero no se deshace: FR-018 deja el viaje inmutable para siempre, para todos los
/// roles. Por eso el primer intento responde <c>409</c> <b>sin cambiar nada</b> y la operación se
/// ejecuta recién con la confirmación explícita.
///
/// El criterio no es la gravedad del aviso: es si el paso se puede deshacer (research §5, SC-007a).
///
/// El sistema <b>no</b> exige que se complete el importe: el viaje sin cargo es válido y se rinde
/// igual. Cancelar deja el viaje <c>en curso</c> con su importe en cero, y se puede completar antes
/// de volver a intentarlo (US4 esc. 7).
///
/// Al rendir, el chofer y el vehículo <b>dejan de estar ocupados</b> por el solo cambio de estado:
/// los índices filtrados dejan de alcanzar al viaje. La asignación se conserva y se sigue viendo —
/// liberar es dejar de ocupar, nunca borrar el dato— (FR-037).
/// </summary>
public class RendirViaje(IRepositorioViajes viajes, TimeProvider reloj)
{
    public async Task<ResultadoViaje> EjecutarAsync(
        int id,
        RendicionRequest? peticion,
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

        // `pendiente → rendido` no existe: la pantalla no lo ofrece y el endpoint lo rechaza igual si
        // se lo invoca a mano (FR-033, US4 esc. 10).
        if (!TransicionesDeViaje.EstaPermitida(viaje.Estado, EstadoViaje.Rendido))
        {
            return new ResultadoViaje(
                ErrorViaje.TransicionNoPermitida,
                NumeroDelViaje: viaje.Numero,
                EstadoActual: NombresDeEstadoViaje.EnTexto(viaje.Estado),
                EstadoPedido: NombresDeEstadoViaje.EnTexto(EstadoViaje.Rendido));
        }

        // FR-038. El primer intento **no aplica el cambio**: responde y el viaje queda exactamente
        // como estaba.
        if (viaje.Importe == 0m && peticion?.Confirmado != true)
        {
            return new ResultadoViaje(
                ErrorViaje.RendicionRequiereConfirmacion,
                NumeroDelViaje: viaje.Numero);
        }

        await viajes.RegistrarCambioDeEstadoAsync(
            viaje,
            EstadoViaje.Rendido,
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
