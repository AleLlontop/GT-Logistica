using GT.Domain.Usuarios;

namespace GT.Domain.Pagos;

public enum TipoOperacionPago : byte
{
    Generar = 0,
    Anular = 1,
}

/// <summary>Resultado confirmado en el mismo commit económico; la clave no se particiona por usuario.</summary>
public class ResultadoOperacionPago
{
    public int Id { get; set; }
    public int UsuarioId { get; set; }
    public Usuario? Usuario { get; set; }
    public TipoOperacionPago Operacion { get; set; }
    public Guid Clave { get; set; }
    public int VersionCanonica { get; set; } = 1;
    public required string ContenidoCanonico { get; set; }
    public required byte[] Hash { get; set; }
    public int OrdenPagoProveedorId { get; set; }
    public OrdenPagoProveedor? OrdenPagoProveedor { get; set; }
    public short EstadoHttp { get; set; }
    public required string ResultadoJson { get; set; }
    public DateTime CompletadaEn { get; set; }
}
