using GT.Domain.Proveedores;

namespace GT.Domain.Pagos;

/// <summary>La anulación libera Vigente, no elimina el vínculo histórico.</summary>
public class OrdenPagoProveedorFactura
{
    public int OrdenPagoProveedorId { get; set; }
    public OrdenPagoProveedor? OrdenPagoProveedor { get; set; }
    public int FacturaProveedorId { get; set; }
    public FacturaProveedor? FacturaProveedor { get; set; }
    public decimal ImporteTotalAlPagar { get; set; }
    public bool Vigente { get; set; } = true;
}
