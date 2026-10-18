namespace GT.Application.Proveedores;

public record DatosFacturaProveedor(int ProveedorId,string? Numero,DateOnly? Fecha,string? Detalle,decimal? Neto,
    string? Cae,DateOnly? FechaVencimientoCae,int? LiquidacionId=null);
public record ArchivoFacturaProveedor(string Nombre,string Url);
public record LiquidacionFacturaProveedorDetalle(int Id,string Numero,string Periodo,decimal ImporteAlRegistrar,decimal ImporteActual,string EstadoActual);
public record DetalleFacturaProveedor(int Id,int ProveedorId,string ProveedorRazonSocial,string ProveedorCuit,
    string TipoProveedorAlRegistrar,string Numero,DateOnly Fecha,string Detalle,decimal Neto,decimal Iva,decimal ImporteTotal,
    string Cae,DateOnly FechaVencimientoCae,string Estado,ArchivoFacturaProveedor? Archivo,LiquidacionFacturaProveedorDetalle? Liquidacion=null);

/// <summary>La escritura pudo haberse confirmado; no compensar el PDF sin comprobar la referencia.</summary>
public class ConfirmacionFacturaInciertaException(int facturaId,Exception interna) : Exception("No se pudo confirmar el resultado del registro.",interna)
{
    public int FacturaId { get; }=facturaId;
}
