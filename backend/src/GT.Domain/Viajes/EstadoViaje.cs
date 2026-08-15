namespace GT.Domain.Viajes;

/// <summary>
/// Estado del viaje, con cinco valores y transiciones cerradas (FR-031, FR-033; Módulo 6 FR-051).
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

    /// <summary>
    /// El viaje ya está incluido en una factura vigente (Módulo 6, FR-051). Terminal e inmutable para
    /// todos los roles, con el mismo alcance que ya regía para <see cref="Rendido"/> (FR-052).
    ///
    /// <b>Va al final del enum y los cuatro anteriores no se reordenan</b>, y no es una preferencia
    /// de estilo: los tres índices filtrados de arriba llevan el <c>1</c> y el <c>3</c> escritos a
    /// mano. Agregar al final no toca ninguno, y el de remito —<c>Estado &lt;&gt; 3</c>— pasa a cubrir
    /// también a los facturados, que es lo correcto: un viaje facturado no libera su remito
    /// (Módulo 6, research §8.1).
    ///
    /// A diferencia de los otros dos terminales, de éste <b>sí</b> se vuelve: anular la factura
    /// devuelve sus viajes a <see cref="Rendido"/> (FR-047 del Módulo 6). Lo hace el caso de uso de
    /// facturación y ningún endpoint del Módulo 5.
    /// </summary>
    Facturado = 4,
}
