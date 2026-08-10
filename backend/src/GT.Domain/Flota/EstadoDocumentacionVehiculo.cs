namespace GT.Domain.Flota;

/// <summary>
/// Estado general de la documentación de <b>un vehículo</b>, con exactamente cuatro valores (FR-033).
/// Es un valor derivado: no se almacena en ninguna columna.
///
/// Se llama <c>EnRegla</c> y no <c>Vigente</c> a propósito, igual que en el chofer: <c>vigente</c>
/// describe un papel, <c>en regla</c> describe una unidad. Mantenerlos con nombres distintos evita
/// que un mismo término signifique dos cosas según dónde aparezca.
/// </summary>
public enum EstadoDocumentacionVehiculo : byte
{
    /// <summary>
    /// El vehículo no tiene ningún documento cargado. <b>No es lo mismo que estar en regla</b>: una
    /// unidad sin papeles no está al día por ausencia de papeles, y no puede quedar disponible
    /// (FR-013, FR-033).
    /// </summary>
    SinDocumentacion = 1,

    /// <summary>Todos sus documentos vigentes están al día.</summary>
    EnRegla = 2,

    /// <summary>Alguno de sus documentos vigentes entra en la ventana de aviso de su tipo.</summary>
    ProximaAvencer = 3,

    /// <summary>Alguno de sus documentos vigentes ya venció.</summary>
    Vencida = 4,
}
