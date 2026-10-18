using GT.Domain.Liquidaciones;

namespace GT.Domain.Proveedores;

/// <summary>Comprobante recibido inmutable: el vínculo y su importe histórico viven en esta fila.</summary>
public class FacturaProveedor
{
    public int Id { get; set; }
    public int ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }
    public TipoProveedor TipoProveedorAlRegistrar { get; set; }
    public required string ProveedorRazonSocial { get; set; }
    public required string ProveedorCuit { get; set; }
    public required string Numero { get; set; }
    public DateOnly Fecha { get; set; }
    public required string Detalle { get; set; }
    public decimal Neto { get; set; }
    public decimal Iva { get; set; }
    public decimal ImporteTotal { get; set; }
    public required string Cae { get; set; }
    public DateOnly FechaVencimientoCae { get; set; }
    public EstadoFacturaProveedor Estado { get; set; } = EstadoFacturaProveedor.Pendiente;
    public int? LiquidacionId { get; set; }
    public Liquidacion? Liquidacion { get; set; }
    public decimal? ImporteLiquidacionAlRegistrar { get; set; }
    public string? ArchivoRuta { get; set; }
    public string? ArchivoNombre { get; set; }
    public string? ArchivoTipoContenido { get; set; }
}
