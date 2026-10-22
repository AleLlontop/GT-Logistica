namespace GT.Domain.Pagos;

public class Cheque : MedioPagoProveedor
{
    public Cheque() => Tipo = TipoMedioPagoProveedor.Cheque;
    public int BancoId { get; set; }
    public Banco? Banco { get; set; }
    public required string NumeroCheque { get; set; }
    public DateOnly FechaVencimiento { get; set; }
    public string? DetalleCheque { get; set; }
    public EstadoCheque EstadoCheque { get; set; } = EstadoCheque.Pendiente;
}
