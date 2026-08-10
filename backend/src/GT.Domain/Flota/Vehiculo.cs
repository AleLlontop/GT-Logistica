using GT.Domain.Choferes;

namespace GT.Domain.Flota;

/// <summary>
/// Unidad de la flota. Entidad principal del módulo (FR-001).
///
/// Todo vehículo <b>pertenece a un transportista</b>, y eso es lo que distingue la flota propia de la
/// contratada: G&amp;T Logística S.A. es un transportista más del padrón del Módulo 3, sin trato
/// especial (FR-008b).
/// </summary>
public class Vehiculo
{
    public int Id { get; set; }

    /// <summary>
    /// Patente <b>ya normalizada</b>: mayúsculas, sin espacios ni guiones ni puntos (FR-003). Única
    /// en toda la flota —incluidos los dados de baja— con un índice único sin filtro por
    /// <see cref="Activo"/>, así que la patente de una unidad dada de baja sigue ocupada (FR-002,
    /// FR-008f).
    ///
    /// Se normaliza <b>antes</b> de validar el formato: si se validara primero, <c>AB-123-CD</c>
    /// sería rechazada por formato en vez de aceptada como la patente que es (research §6).
    /// </summary>
    public required string Patente { get; set; }

    /// <summary>Obligatoria, con <c>Trim</c> al guardar (FR-006).</summary>
    public required string Marca { get; set; }

    /// <summary>Obligatorio, con <c>Trim</c> al guardar (FR-006).</summary>
    public required string Modelo { get; set; }

    /// <summary>Tiene que estar activo al crear y al modificar (FR-005).</summary>
    public required int TipoVehiculoId { get; set; }

    public TipoVehiculo? Tipo { get; set; }

    /// <summary>Tiene que estar activo al crear y al modificar (FR-008a).</summary>
    public required int TransportistaId { get; set; }

    public Transportista? Transportista { get; set; }

    /// <summary>
    /// <b>Lo que eligió el operador</b>, no lo que el listado muestra (FR-012). El valor mostrado se
    /// deriva al consultar y esta columna no se sobrescribe nunca: al renovar el documento vencido,
    /// la unidad vuelve a estar disponible sola, sin proceso nocturno (FR-014, research §4).
    /// </summary>
    public required VehiculoEstado EstadoOperativo { get; set; }

    /// <summary>
    /// <c>false</c> es la baja lógica (FR-001). Una unidad dada de baja no aparece en el listado sin
    /// filtros ni en el panel de vencimientos, conserva intacta su documentación con sus archivos
    /// (FR-008) y puede reactivarse (FR-008e).
    /// </summary>
    public bool Activo { get; set; } = true;

    /// <summary>
    /// Documentación de la unidad, vigente e histórica. Puede estar vacía, y es un caso válido: el
    /// vehículo figura como <c>sin documentación</c>, que no es lo mismo que estar en regla (FR-033).
    /// </summary>
    public ICollection<DocumentacionVehiculo> Documentacion { get; } = [];
}
