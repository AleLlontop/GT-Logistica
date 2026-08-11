namespace GT.Domain.Viajes;

/// <summary>
/// Veredicto de habilitación de una unidad —chofer o vehículo— para un viaje (FR-022 a FR-024).
///
/// <b>Derivado y nunca almacenado</b>: no hay columna que lo guarde. Se calcula al asignar, contra la
/// <b>fecha del viaje</b> y no contra el día en curso, y se devuelve con el resultado de esa
/// operación. Guardarlo obligaría a recalcularlo cada vez que se toca un documento en los Módulos 3
/// y 4, que este módulo no controla.
/// </summary>
public enum HabilitacionAsignacion
{
    /// <summary>
    /// Todos los documentos vigentes a la fecha del viaje, <b>o ninguno cargado</b>: la ausencia de
    /// documentación no prohíbe nada, porque el sistema no infiere que falte un papel que nadie cargó
    /// (FR-024).
    /// </summary>
    Habilitado,

    /// <summary>Ninguno vencido y alguno dentro de su ventana de aviso. <b>Se guarda igual</b> (FR-023).</summary>
    ConAdvertencia,

    /// <summary>Alguno vencido a la fecha del viaje. <b>No se guarda nada</b> (FR-022).</summary>
    Bloqueado,
}
