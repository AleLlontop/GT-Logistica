namespace GT.Domain.Pagos;

public class Efectivo : MedioPagoProveedor
{
    public Efectivo() => Tipo = TipoMedioPagoProveedor.Efectivo;
}
