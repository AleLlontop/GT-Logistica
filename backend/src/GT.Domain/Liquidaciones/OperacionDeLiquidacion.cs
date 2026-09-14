namespace GT.Domain.Liquidaciones;

/// <summary>
/// Qué registra cada entrada del historial de la liquidación (FR-033).
///
/// <b>⚠ <c>IX_CambiosDeLiquidacion_Unica</c> lleva el <c>1</c> de <see cref="Edicion"/> escrito a
/// mano</b> en su filtro: una sola generación, una sola anulación y un solo paso a pagada por
/// liquidación, y ediciones las que hagan falta. Reordenar el enum no falla al compilar.
/// </summary>
public enum OperacionDeLiquidacion : byte
{
    Generacion = 0,

    /// <summary>La única operación que se repite, y la única con viajes quitados y agregados.</summary>
    Edicion = 1,

    Anulacion = 2,

    /// <summary>
    /// El paso a pagada, con el usuario y el instante de la orden de pago que dejó el saldo en cero.
    /// Las órdenes parciales no agregan entrada: ya las lista su propia sección del detalle.
    /// </summary>
    Pagada = 3,
}
