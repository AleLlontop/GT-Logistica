using GT.Domain.Personas;

namespace GT.Domain.Choferes;

/// <summary>
/// Persona habilitada para conducir en nombre de un transportista (FR-005).
///
/// <b>Composición, no herencia</b> (research §1): el chofer no hereda de <see cref="Persona"/>, la
/// referencia con una clave foránea única. Es lo que permite que alguien ya cargado como empleado se
/// registre como chofer reutilizando su fila, un caso límite explícito de la spec que la herencia de
/// EF Core no admite —el discriminador de una entidad seguida no se puede cambiar—.
///
/// Los datos personales viven en <see cref="Persona"/> y <b>no se duplican acá</b> (FR-006): nombre,
/// apellido, DNI, teléfono, email y fecha de nacimiento se leen de la persona referenciada. Esta
/// entidad agrega sólo lo propio del chofer.
///
/// La fila en esta tabla es la <b>única fuente de verdad</b> sobre quién es chofer;
/// <c>Persona.Tipo</c> queda como dato informativo del padrón y este módulo no lo consulta.
/// </summary>
public class Chofer
{
    public int Id { get; set; }

    /// <summary>
    /// Persona del padrón del Módulo 2. Único: una persona es chofer a lo sumo una vez, activo o
    /// inactivo. Por eso volver a registrar como chofer a una persona que ya lo es se rechaza, y
    /// para el que vuelve a trabajar existe la reactivación (FR-005b).
    /// </summary>
    public required int PersonaId { get; set; }

    public Persona? Persona { get; set; }

    /// <summary>
    /// Sólo dígitos, once, con dígito verificador válido (FR-007, FR-025). Único en todo el padrón,
    /// garantizado con un índice en la base.
    /// </summary>
    public required string Cuil { get; set; }

    /// <summary>
    /// Transportista al que pertenece. Obligatorio de verdad —<c>NOT NULL</c> en la base, no sólo
    /// por código— y tiene que estar activo al asignarlo (FR-008).
    /// </summary>
    public required int TransportistaId { get; set; }

    public Transportista? Transportista { get; set; }

    /// <summary>
    /// <c>false</c> es la baja lógica (FR-005). Un chofer inactivo no aparece en el listado sin
    /// filtros ni en el panel de vencimientos (FR-021, FR-022), conserva intacta su documentación
    /// (FR-005a) y puede reactivarse (FR-005b).
    /// </summary>
    public bool Activo { get; set; } = true;

    /// <summary>
    /// Documentación del chofer, vigente e histórica. Puede estar vacía, y es un caso válido: el
    /// chofer figura como <c>sin documentación</c>, que no es lo mismo que estar en regla (FR-028).
    /// </summary>
    public ICollection<Documentacion> Documentacion { get; } = [];
}
