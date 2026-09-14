using GT.Domain.Viajes;

namespace GT.Domain.Liquidaciones;

/// <summary>
/// Un viaje que una edición quitó o agregó (FR-033, research §3b).
///
/// <b>Guarda la referencia al viaje, no su número copiado</b>: el número de un viaje no cambia nunca y
/// un viaje no se borra. Tampoco guarda importes: con la composición actual y los cambios de cada
/// edición, la de cualquier momento anterior se reconstruye.
/// </summary>
public class CambioDeLiquidacionViaje
{
    public required int CambioDeLiquidacionId { get; set; }

    public CambioDeLiquidacion? Cambio { get; set; }

    public required int ViajeId { get; set; }

    public Viaje? Viaje { get; set; }

    /// <summary><c>true</c> si la edición lo agregó, <c>false</c> si lo quitó.</summary>
    public required bool Agregado { get; set; }
}
