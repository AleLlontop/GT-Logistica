namespace GT.Domain.Choferes;

/// <summary>
/// Estado de <b>un documento</b>, calculado por el sistema y nunca almacenado (FR-017, research §2).
/// Ningún usuario puede elegirlo ni editarlo por ninguna vía (FR-018).
///
/// No confundir con el estado general del chofer, que tiene cuatro valores y se llama distinto a
/// propósito: <c>vigente</c> describe un papel, <c>enRegla</c> describe a una persona
/// (<see cref="EstadoDocumentacionChofer"/>).
/// </summary>
public enum DocumentacionEstado : byte
{
    /// <summary>Faltan más días para el vencimiento que los de aviso de su tipo.</summary>
    Vigente = 1,

    /// <summary>El vencimiento cae entre hoy inclusive y la ventana de aviso de su tipo.</summary>
    ProximaAvencer = 2,

    /// <summary>La fecha de vencimiento ya pasó.</summary>
    Vencida = 3,
}
