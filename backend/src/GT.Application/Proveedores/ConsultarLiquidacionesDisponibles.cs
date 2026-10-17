namespace GT.Application.Proveedores;

public record LiquidacionDisponibleProveedor(int Id, string Numero, string Periodo, decimal ImporteTotal);

public class ConsultarLiquidacionesDisponibles(IRepositorioFacturasProveedor repositorio)
{
    public Task<IReadOnlyList<LiquidacionDisponibleProveedor>> EjecutarAsync(int proveedorId, CancellationToken cancelacion) =>
        repositorio.ConsultarLiquidacionesDisponiblesAsync(proveedorId, cancelacion);
}

public class ImportesLiquidacionDistintosException(decimal factura, decimal liquidacion) : ProveedorException(
    "importe_distinto",
    $"El total de la factura ({factura.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("es-AR"))}) no coincide con el total actual de la liquidación ({liquidacion.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("es-AR"))}). Corregí el neto antes de guardar.",
    "liquidacionId", TipoErrorProveedor.Conflicto)
{
    public decimal ImporteTotalFactura { get; } = factura;
    public decimal ImporteTotalLiquidacion { get; } = liquidacion;
}
