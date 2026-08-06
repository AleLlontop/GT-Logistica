namespace GT.Domain.Choferes;

/// <summary>
/// Estado general de la documentación de <b>un chofer</b>, con exactamente cuatro valores (FR-029).
/// Es un valor derivado: no se almacena.
///
/// Se llama <c>EnRegla</c> y no <c>Vigente</c> a propósito: <c>vigente</c> describe un papel,
/// <c>en regla</c> describe a una persona. Mantenerlos con nombres distintos evita que un mismo
/// término signifique dos cosas según dónde aparezca.
/// </summary>
public enum EstadoDocumentacionChofer : byte
{
    /// <summary>
    /// El chofer no tiene ningún documento cargado. <b>No es lo mismo que estar en regla</b>: un
    /// chofer sin papeles no está al día por ausencia de papeles (FR-028).
    /// </summary>
    SinDocumentacion = 1,

    /// <summary>Todos sus documentos vigentes están al día.</summary>
    EnRegla = 2,

    /// <summary>Alguno de sus documentos vigentes entra en la ventana de aviso de su tipo.</summary>
    ProximaAvencer = 3,

    /// <summary>Alguno de sus documentos vigentes ya venció.</summary>
    Vencida = 4,
}
