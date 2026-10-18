namespace GT.Application.Proveedores;

public record ProveedorPeticion(string? Tipo, string? RazonSocial = null, string? Cuit = null,
    string? Telefono = null, string? Email = null, string? NombreComercial = null,
    string? Direccion = null, int? TransportistaId = null, long? Version = null);
public record VersionProveedor(long Version);
public record FiltrosProveedor(string? Q = null, string? RazonSocial = null, string? NombreComercial = null,
    string? Cuit = null, string? Tipo = null, string? Estado = null, int Pagina = 1);
public record ProveedorResumen(int Id, string RazonSocial, string? NombreComercial, string Cuit, string Tipo, string Estado);
public record ProveedorDetalle
{
    public int Id { get; init; }
    public required string RazonSocial { get; init; }
    public string? NombreComercial { get; init; }
    public required string Cuit { get; init; }
    public required string Tipo { get; init; }
    public required string Estado { get; init; }
    public string? Telefono { get; init; }
    public string? Email { get; init; }
    public string? Direccion { get; init; }
    public int? TransportistaId { get; init; }
    public long Version { get; init; }
}
public record TransportistaExterno(int Id, string RazonSocial, string Cuit);
public enum TipoErrorProveedor { Datos, Conflicto, NoEncontrado }
public class ProveedorException(string codigo, string mensaje, string? campo = null,
    TipoErrorProveedor tipo = TipoErrorProveedor.Datos) : Exception(mensaje)
{
    public string Codigo { get; } = codigo;
    public string? Campo { get; } = campo;
    public TipoErrorProveedor Tipo { get; } = tipo;
    public static ProveedorException Duplicado() => new("cuit_duplicado", "Ese CUIT ya está registrado para otro proveedor, incluso si está inactivo.", "cuit");
    public static ProveedorException NoEncontrado() => new("no_encontrado", "No encontramos ese proveedor.", tipo: TipoErrorProveedor.NoEncontrado);
    public static ProveedorException Cambio() => new("proveedor_modificado", "El proveedor cambió. Volvé a abrirlo antes de guardar.", tipo: TipoErrorProveedor.Conflicto);
}

