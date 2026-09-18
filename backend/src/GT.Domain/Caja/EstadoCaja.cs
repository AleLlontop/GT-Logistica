namespace GT.Domain.Caja;

/// <summary>
/// Estado de la caja: dos valores excluyentes (FR-001, FR-019).
///
/// Toda caja nace <see cref="Abierta"/> y la única transición es a <see cref="Cerrada"/>, que es final:
/// no se reabre (spec §Assumptions).
///
/// <b>⚠ Los números importan y no son un detalle de serialización.</b> <c>CK_Cajas_CierreConsistente</c> e
/// <c>IX_Cajas_UsuarioResponsable_Abierta</c> llevan <c>0</c> y <c>1</c> escritos a mano: reordenar este
/// enum <b>no falla al compilar</b> y deja al <c>CHECK</c> y al índice protegiendo el estado equivocado. Lo
/// cubre <c>RestriccionesDeCajaTests</c> (data-model §Enumeraciones).
/// </summary>
public enum EstadoCaja : byte
{
    /// <summary>Admite movimientos. Sin fecha de cierre ni saldo final, y la base lo exige.</summary>
    Abierta = 0,

    /// <summary>Con fecha de cierre y saldo final, y la base lo exige. No admite movimientos (FR-019).</summary>
    Cerrada = 1,
}
