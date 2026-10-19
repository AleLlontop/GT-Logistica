namespace GT.Domain.Pagos;

public enum EstadoOrdenPagoProveedor : byte
{
    Pendiente = 0,
    Anulada = 1,
}

public enum EstadoCheque : byte
{
    Pendiente = 0,
    Entregado = 1,
    Depositado = 2,
    Cobrado = 3,
    Rechazado = 4,
    Anulado = 5,
}

public enum TipoMedioPagoProveedor : byte
{
    Cheque = 0,
    Transferencia = 1,
    Efectivo = 2,
}
