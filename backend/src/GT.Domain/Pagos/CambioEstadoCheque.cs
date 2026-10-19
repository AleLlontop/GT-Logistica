using GT.Domain.Usuarios;

namespace GT.Domain.Pagos;

/// <summary>Historia inmutable de cambios explícitos; Tipo permite cerrar la FK sólo a cheques.</summary>
public class CambioEstadoCheque
{
    public long Id { get; set; }
    public int ChequeId { get; set; }
    public TipoMedioPagoProveedor Tipo { get; set; } = TipoMedioPagoProveedor.Cheque;
    public MedioPagoProveedor? Cheque { get; set; }
    public EstadoCheque EstadoAnterior { get; set; }
    public EstadoCheque EstadoNuevo { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public DateTime OcurridoEn { get; set; }
}
