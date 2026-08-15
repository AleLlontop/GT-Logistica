namespace GT.Domain.Facturacion;

/// <summary>
/// Clase de comprobante (FR-008). Determina la alícuota de IVA y el código de letra del documento,
/// y es <b>inmutable después de emitir</b> (FR-036).
/// </summary>
public enum TipoComprobante : byte
{
    FacturaA = 0,
    FacturaB = 1,

    /// <summary>IVA cero: el total es igual al neto, y no es un error (FR-023).</summary>
    FacturaC = 2,
}

/// <summary>
/// Si la factura es la primera por ese trabajo o reemplaza a una anulada (FR-009).
///
/// Con <see cref="Refacturacion"/> la referencia a la factura reemplazada es <b>obligatoria</b>, y con
/// <see cref="Original"/> está <b>prohibida</b> (FR-049).
/// </summary>
public enum TipoFacturacion : byte
{
    Original = 0,
    Refacturacion = 1,
}

/// <summary>
/// Forma de pago acordada (FR-009a). <b>Es dato de la factura, no del cliente</b>: el mismo cliente
/// puede tener una factura al contado y la siguiente en cuenta corriente.
/// </summary>
public enum CondicionDeVenta : byte
{
    Contado = 0,
    CuentaCorriente = 1,

    /// <summary>Se muestra como <c>Tarjeta de Débito / Crédito</c> (contracts/README).</summary>
    Tarjeta = 2,

    Cheque = 3,
}
