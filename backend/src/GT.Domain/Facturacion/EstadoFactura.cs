namespace GT.Domain.Facturacion;

/// <summary>
/// Lo que la factura <b>guarda</b> en su columna <c>Estado</c>: exactamente tres valores.
///
/// <b><c>vencida</c> no está acá y no es un olvido</b>: se deriva al leer comparando el vencimiento de
/// pago con el día en curso, y no hay proceso que la escriba (FR-041). Lo que se ve y lo que se filtra
/// es <see cref="EstadoFacturaVisible"/>.
///
/// <b>⚠ Los números importan y no son un detalle de serialización.</b> Los dos índices únicos
/// filtrados de la tabla <c>Facturas</c> llevan estos valores escritos a mano en su <c>WHERE</c>:
///
/// <list type="bullet">
///   <item><c>IX_Facturas_Numero … WHERE [Estado] &lt;&gt; 2</c> (anulada) — FR-027</item>
/// </list>
///
/// Reordenar este enum <b>no falla al compilar</b> y deja el índice protegiendo el estado equivocado:
/// el número de comprobante pasaría a ser único entre las anuladas y dos facturas vigentes podrían
/// compartirlo. Eso lo cubre <c>IndicesDeFacturaTests</c>, que inserta una fila en cada estado y
/// verifica dónde el índice acepta y dónde rechaza (research §4, §15.2).
/// </summary>
public enum EstadoFactura : byte
{
    /// <summary>Toda factura nace acá, con su entrada de historial (FR-040).</summary>
    Pendiente = 0,

    /// <summary>Terminal. No existe ninguna acción que revierta un cobro (FR-043).</summary>
    Pagada = 1,

    /// <summary>Terminal. Ni se corrige ni vuelve a <c>pendiente</c> (FR-038, FR-043).</summary>
    Anulada = 2,
}
