namespace GT.Domain.Pagos;

/// <summary>Medio de una OPP confirmada. La edición del formulario nunca crea esta fila por separado.</summary>
public abstract class MedioPagoProveedor
{
    public int Id { get; set; }
    public int OrdenPagoProveedorId { get; set; }
    public OrdenPagoProveedor? OrdenPagoProveedor { get; set; }
    public Guid RenglonId { get; set; }
    public TipoMedioPagoProveedor Tipo { get; protected set; }
    public decimal Importe { get; set; }
    /// <summary>Emisión de cheque, fecha de transferencia o día argentino efectivo.</summary>
    public DateOnly Fecha { get; set; }
}
