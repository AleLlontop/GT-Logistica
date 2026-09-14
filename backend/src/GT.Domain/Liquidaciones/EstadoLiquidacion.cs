namespace GT.Domain.Liquidaciones;

/// <summary>
/// Estado de la liquidación: tres valores excluyentes (FR-029).
///
/// Toda liquidación nace <see cref="Pendiente"/> (FR-015), pasa sola a <see cref="Pagada"/> cuando no
/// resta nada por pagar (FR-030, FR-041) y a <see cref="Anulada"/> por la anulación (FR-057). Los dos
/// últimos son finales (FR-031).
///
/// <b>⚠ Los números importan y no son un detalle de serialización.</b> <c>CK_Liquidaciones_Estado</c>
/// lleva <c>0</c>, <c>1</c> y <c>2</c> escritos a mano: reordenar este enum <b>no falla al compilar</b> y
/// deja al <c>CHECK</c> protegiendo el estado equivocado. Lo cubre <c>RestriccionesDeLiquidacionTests</c>
/// (research §12.1).
/// </summary>
public enum EstadoLiquidacion : byte
{
    Pendiente = 0,

    /// <summary>Lo pagado alcanzó el total. La base exige <c>ImportePagado = ImporteTotal</c>.</summary>
    Pagada = 1,

    /// <summary>Con motivo y sin pagos. Sus vínculos quedan con <c>Vigente = 0</c>.</summary>
    Anulada = 2,
}
