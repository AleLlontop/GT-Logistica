using GT.Domain.Viajes;

namespace GT.Domain.Liquidaciones;

/// <summary>
/// Un viaje agrupado en una liquidación (FR-014, FR-028).
///
/// <b>Tabla propia y no una columna en <c>Viajes</c></b>: una columna tocaría el Módulo 5 y, al anular,
/// perdería qué viajes agrupaba la liquidación (research §1).
/// </summary>
public class LiquidacionViaje
{
    public required int LiquidacionId { get; set; }

    public Liquidacion? Liquidacion { get; set; }

    public required int ViajeId { get; set; }

    public Viaje? Viaje { get; set; }

    /// <summary>
    /// Vale <c>false</c> <b>exactamente</b> cuando la liquidación está anulada.
    ///
    /// Repite un dato del estado, y se acepta porque un índice filtrado sólo mira columnas de su propia
    /// tabla: <c>IX_LiquidacionViajes_ViajeVigente</c> es lo que garantiza en la base que un viaje no esté
    /// en dos liquidaciones vigentes. Se escribe en la misma transacción que la anulación y lo verifica
    /// <c>CoherenciaDeLiquidacionTests</c>.
    /// </summary>
    public bool Vigente { get; set; } = true;
}
