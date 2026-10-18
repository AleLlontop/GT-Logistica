using GT.Domain.Choferes;

namespace GT.Domain.Proveedores;

/// <summary>El fletero sólo referencia identidad/contacto de Transportistas (013 FR-006).</summary>
public class Proveedor
{
    public int Id { get; set; }
    public TipoProveedor Tipo { get; set; }
    public bool Activo { get; set; } = true;
    public long Version { get; set; } = 1;
    public int? TransportistaId { get; set; }
    public Transportista? Transportista { get; set; }
    public string? RazonSocial { get; set; }
    public string? Cuit { get; set; }
    public string? Telefono { get; set; }
    public string? Email { get; set; }
    public string? NombreComercial { get; set; }
    public string? Direccion { get; set; }
}
