using GT.Domain.Proveedores;
using GT.Domain.Usuarios;

namespace GT.Domain.Pagos;

/// <summary>Documento administrativo de 014; nunca es una OrdenDePago de 009.</summary>
public class OrdenPagoProveedor
{
    public int Id { get; set; }
    public int Numero { get; private set; }
    public DateOnly Fecha { get; set; }
    public string? Detalle { get; set; }
    public int ProveedorId { get; set; }
    public Proveedor? Proveedor { get; set; }
    public TipoProveedor TipoProveedorAlRegistrar { get; set; }
    public required string ProveedorRazonSocial { get; set; }
    public required string ProveedorCuit { get; set; }
    public bool ProveedorActivoAlGenerar { get; set; }
    public decimal ImporteTotal { get; set; }
    public EstadoOrdenPagoProveedor Estado { get; set; } = EstadoOrdenPagoProveedor.Pendiente;
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public DateTime RegistradaEn { get; set; }
    public string? MotivoAnulacion { get; set; }
    public int? AnuladaPorId { get; set; }
    public Usuario? AnuladaPor { get; set; }
    public DateTime? AnuladaEn { get; set; }
    public ICollection<MedioPagoProveedor> Medios { get; } = [];
    public ICollection<OrdenPagoProveedorFactura> Facturas { get; } = [];
}
