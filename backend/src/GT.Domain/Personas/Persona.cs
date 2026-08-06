namespace GT.Domain.Personas;

/// <summary>
/// Chofer o empleado de G&amp;T Logística.
///
/// El padrón se registra y se mantiene desde este mismo módulo y <b>arranca vacío</b>: no se siembra
/// por migración (FR-024). Una persona puede asociarse opcionalmente a un único usuario a la vez
/// (FR-008).
///
/// Son exactamente los siete datos que pide FR-026, más el estado de la baja lógica. No agregar
/// campos acá sin cambiar antes la spec: la lista es taxativa.
/// </summary>
public class Persona
{
    public int Id { get; set; }

    public required string Nombre { get; set; }

    public required string Apellido { get; set; }

    /// <summary>Sólo dígitos. Es el único dato con restricción de unicidad en el padrón (FR-027).</summary>
    public required string Dni { get; set; }

    public required TipoIntegrante Tipo { get; set; }

    public required string Telefono { get; set; }

    /// <summary>Con formato válido, pero <b>sin</b> restricción de unicidad (FR-027).</summary>
    public required string Email { get; set; }

    public required DateOnly FechaNacimiento { get; set; }

    /// <summary>
    /// <c>false</c> es la baja lógica: el registro no se borra nunca (FR-022) y la persona deja de
    /// ofrecerse para asociar a un usuario (FR-023).
    ///
    /// Es un <c>bool</c> y no un enum a propósito: la spec le da a la persona exactamente dos
    /// estados, a diferencia de los tres del usuario (research §6).
    /// </summary>
    public bool Activa { get; set; } = true;

    /// <summary>
    /// Datos de chofer de esta persona, si los tiene (Módulo 3). Es una navegación, no un dato del
    /// padrón: los siete campos de arriba siguen siendo taxativos.
    ///
    /// Su presencia —y no <see cref="Tipo"/>— es la única fuente de verdad sobre quién es chofer
    /// (research §1 del Módulo 3). También es lo que impide dar de baja a una persona que tiene un
    /// chofer asociado.
    /// </summary>
    public Choferes.Chofer? Chofer { get; set; }

    /// <summary>Nombre completo para mostrar, en el orden habitual en Argentina.</summary>
    public string NombreCompleto => $"{Apellido}, {Nombre}";
}
