namespace GT.Domain.Pagos;

public class Transferencia : MedioPagoProveedor
{
    public Transferencia() => Tipo = TipoMedioPagoProveedor.Transferencia;
    public required string CbuOrigen { get; set; }
    public required string CbuDestino { get; set; }
    public required string NumeroComprobante { get; set; }
    public string? Observaciones { get; set; }
}
