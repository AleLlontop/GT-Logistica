using GT.Domain.Usuarios;

namespace GT.Domain.Viajes;

/// <summary>
/// Una línea del historial de cambios de estado de un viaje (FR-035).
///
/// <b>No se edita ni se borra por ninguna vía</b>: ningún endpoint la escribe directamente ni la
/// modifica, y esta clase no expone nada que permita cambiarla después de creada. Se escribe siempre
/// en la misma transacción que el cambio de estado que registra.
///
/// Guarda quién y cuándo, y <b>nada más</b>: no registra qué datos se editaron ni desde dónde, porque
/// FR-035 no lo pide y sería recolectar por las dudas (Principio V).
/// </summary>
public class CambioDeEstadoViaje
{
    public int Id { get; init; }

    public required int ViajeId { get; init; }

    public Viaje? Viaje { get; init; }

    /// <summary>
    /// <c>null</c> <b>sólo</b> en el registro del alta: antes del alta no había estado. En la pantalla
    /// se lee como <c>Alta → Pendiente</c>.
    /// </summary>
    public EstadoViaje? EstadoAnterior { get; init; }

    public required EstadoViaje EstadoNuevo { get; init; }

    /// <summary>
    /// Usuario de la sesión que produjo el cambio. Llega <b>por parámetro</b> desde el endpoint, que
    /// lo lee con <c>ClaimsSesion.ObtenerIdUsuario</c>: no se introduce una abstracción de usuario
    /// actual que hoy tendría cuatro llamadores (research §7).
    /// </summary>
    public required int UsuarioId { get; init; }

    public Usuario? Usuario { get; init; }

    /// <summary>
    /// Instante en UTC puesto por el servidor con <c>TimeProvider</c>. Sale del API con la <c>Z</c>
    /// por la conversión declarada una sola vez en <c>GtDbContext</c> (convención [002]).
    /// </summary>
    public required DateTime OcurridoEn { get; init; }
}
