using GT.Domain.Usuarios;

namespace GT.Domain.Adelantos;

/// <summary>
/// Una entrada del historial del adelanto: registro, aprobación, rechazo o anulación (FR-036).
///
/// <b>Sin columna de motivo</b>: el del rechazo y el de la anulación se leen de
/// <see cref="Adelanto.MotivoRechazo"/> y <see cref="Adelanto.MotivoAnulacion"/>. Hay uno solo de cada uno
/// posible, y una copia podría discrepar.
///
/// <b>No se edita ni se borra por ninguna vía</b>: la escriben los casos de uso, en la misma transacción
/// que la operación que registra.
/// </summary>
public class CambioDeAdelanto
{
    public int Id { get; init; }

    public required int AdelantoId { get; init; }

    public Adelanto? Adelanto { get; init; }

    public required OperacionDeAdelanto Operacion { get; init; }

    /// <summary>Usuario de la sesión. Llega por parámetro desde el endpoint.</summary>
    public required int UsuarioId { get; init; }

    public Usuario? Usuario { get; init; }

    /// <summary>Instante UTC con <c>TimeProvider</c>. Sale del API con la <c>Z</c> (convención [002]).</summary>
    public required DateTime OcurridoEn { get; init; }
}
