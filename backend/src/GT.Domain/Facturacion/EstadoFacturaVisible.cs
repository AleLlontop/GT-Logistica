namespace GT.Domain.Facturacion;

/// <summary>
/// Lo que se ve en la pantalla y lo que se filtra en el listado.
///
/// <b>No es columna de ninguna tabla.</b> Sale de <see cref="DerivadorEstadoFactura"/> combinando el
/// <see cref="EstadoFactura"/> guardado con el vencimiento de pago y el día en curso (FR-041).
///
/// Sus cuatro valores son <b>excluyentes</b>: una factura impaga y pasada de fecha sale bajo
/// <see cref="Vencida"/> y <b>no</b> bajo <see cref="Pendiente"/>. Si salieran bajo los dos, el filtro
/// contradiría a la columna que la propia fila muestra (FR-058a, US3 esc. 11).
/// </summary>
public enum EstadoFacturaVisible : byte
{
    /// <summary>Impaga y dentro del plazo de pago.</summary>
    Pendiente = 0,

    /// <summary>Impaga y con el vencimiento de pago ya pasado. El único derivado de los cuatro.</summary>
    Vencida = 1,

    Pagada = 2,

    Anulada = 3,
}
