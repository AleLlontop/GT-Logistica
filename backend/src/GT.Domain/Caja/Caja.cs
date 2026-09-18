using GT.Domain.Usuarios;

namespace GT.Domain.Caja;

/// <summary>
/// El efectivo que un empleado administrativo maneja en el día (FR-001 a FR-022). Entidad principal del
/// Módulo 11.
///
/// <b>No se reabre ni se modifica después de cerrada</b> (spec §Assumptions): el cierre la pasa a
/// <see cref="EstadoCaja.Cerrada"/> con su fecha y su saldo final, con un <c>UPDATE</c> condicional del
/// repositorio que no pasa por esta clase (research §2).
///
/// <b>No tiene número visible</b>: se nombra por su responsable, su fecha de apertura y su estado
/// (spec §Assumptions).
/// </summary>
public class Caja
{
    public int Id { get; init; }

    /// <summary>Mayor o igual que cero (RN4, FR-002). <c>decimal</c>, nunca flotante.</summary>
    public required decimal SaldoInicial { get; init; }

    /// <summary>Instante UTC del servidor, con <c>TimeProvider</c>; nunca tipeado (FR-005).</summary>
    public required DateTime FechaApertura { get; init; }

    /// <summary>
    /// Quien la abrió, y el único que registra movimientos y la cierra (FR-001, FR-035). Sin colección
    /// inversa en <see cref="Usuario"/>: el Módulo 2 no se toca.
    /// </summary>
    public required int UsuarioResponsableId { get; init; }

    public Usuario? UsuarioResponsable { get; init; }

    /// <summary>
    /// Nace abierta (FR-001). La columna no tiene <c>DEFAULT</c>: EF manda siempre este valor.
    /// </summary>
    public EstadoCaja Estado { get; init; } = EstadoCaja.Abierta;

    /// <summary>Con valor exactamente cuando está cerrada. La base lo exige.</summary>
    public DateTime? FechaCierre { get; init; }

    /// <summary>Con valor exactamente cuando está cerrada. La base lo exige.</summary>
    public decimal? SaldoFinal { get; init; }

    public ICollection<MovimientoDeCaja> Movimientos { get; } = [];
}
