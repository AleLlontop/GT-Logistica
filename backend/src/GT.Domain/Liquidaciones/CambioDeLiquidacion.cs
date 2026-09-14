using GT.Domain.Usuarios;

namespace GT.Domain.Liquidaciones;

/// <summary>
/// Una entrada del historial de la liquidación: generación, edición, anulación o paso a pagada (FR-033).
///
/// <b>Sin columna de motivo</b>: el de la anulación se lee de <see cref="Liquidacion.MotivoAnulacion"/>.
/// Hay una sola anulación posible y una copia podría discrepar.
///
/// <b>No se edita ni se borra por ninguna vía</b>: la escriben los casos de uso, en la misma transacción
/// que la operación que registra.
/// </summary>
public class CambioDeLiquidacion
{
    public int Id { get; set; }

    public required int LiquidacionId { get; set; }

    public Liquidacion? Liquidacion { get; set; }

    public required OperacionDeLiquidacion Operacion { get; set; }

    public required int UsuarioId { get; set; }

    public Usuario? Usuario { get; set; }

    /// <summary>
    /// Instante UTC con <c>TimeProvider</c>. El de la entrada <c>generacion</c>, pasado a fecha de
    /// Argentina, <b>es</b> la fecha de generación de la liquidación (FR-017, research §12.8).
    /// </summary>
    public required DateTime OcurridoEn { get; set; }

    /// <summary>Qué viajes quitó y agregó. Sólo tiene filas en una <c>edicion</c> (research §3b).</summary>
    public ICollection<CambioDeLiquidacionViaje> Viajes { get; } = [];
}
