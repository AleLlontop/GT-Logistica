namespace GT.Domain.Viajes;

/// <summary>
/// Estado del viaje, con exactamente cuatro valores y transiciones cerradas (FR-031, FR-033).
///
/// <b>⚠ Los números importan y no son un detalle de serialización.</b> Los tres índices únicos
/// filtrados de la tabla <c>Viajes</c> llevan estos valores escritos a mano en su <c>WHERE</c>:
///
/// <list type="bullet">
///   <item><c>IX_Viajes_NumeroRemito … WHERE Estado &lt;&gt; 3</c> (anulado)</item>
///   <item><c>IX_Viajes_ChoferEnCurso … WHERE Estado = 1</c> (en curso)</item>
///   <item><c>IX_Viajes_VehiculoEnCurso … WHERE Estado = 1</c> (en curso)</item>
/// </list>
///
/// Reordenar este enum <b>no falla al compilar</b> y deja los tres índices protegiendo el estado
/// equivocado: el remito volvería a ser único entre los rendidos y dos viajes podrían compartir
/// chofer. Eso lo cubre <c>IndicesFiltradosTests</c>, que inserta un viaje en cada estado y verifica
/// dónde acepta y dónde rechaza cada índice (research §2, §15).
/// </summary>
public enum EstadoViaje : byte
{
    /// <summary>Todo viaje nace acá (FR-032).</summary>
    Pendiente = 0,

    /// <summary>El único estado que ocupa al chofer y al vehículo (FR-026, FR-027).</summary>
    EnCurso = 1,

    /// <summary>Terminal e inmutable para todos los roles (FR-018, SC-013).</summary>
    Rendido = 2,

    /// <summary>Terminal. No cuenta como trabajo realizado y no figura en ningún total (FR-047).</summary>
    Anulado = 3,
}
