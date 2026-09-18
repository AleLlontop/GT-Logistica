namespace GT.Domain.Caja;

/// <summary>
/// Si el movimiento suma o resta al saldo de la caja (FR-006).
///
/// El tipo decide qué referencia admite: un ingreso sólo una factura, un egreso sólo una orden de pago
/// (RN8).
///
/// <b>⚠ Los números importan.</b> <c>CK_MovimientosDeCaja_Referencia</c> lleva <c>0</c> y <c>1</c> escritos
/// a mano: reordenar este enum no falla al compilar y deja al <c>CHECK</c> admitiendo la referencia
/// cruzada (research §6).
/// </summary>
public enum TipoMovimientoCaja : byte
{
    /// <summary>Suma al saldo. Admite una factura; nunca una orden de pago.</summary>
    Ingreso = 0,

    /// <summary>Resta al saldo. Admite una orden de pago; nunca una factura.</summary>
    Egreso = 1,
}
