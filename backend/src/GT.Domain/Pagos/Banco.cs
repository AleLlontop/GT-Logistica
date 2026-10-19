namespace GT.Domain.Pagos;

/// <summary>Padrón de Pagos con identidad estable y baja reversible; no representa una cuenta bancaria.</summary>
public class Banco
{
    public int Id { get; set; }
    public required string Nombre { get; set; }
    public bool Activo { get; set; } = true;
    public int Version { get; set; }
}
