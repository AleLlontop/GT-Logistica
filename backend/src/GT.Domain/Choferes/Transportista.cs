namespace GT.Domain.Choferes;

/// <summary>
/// Empresa o persona que aporta choferes a la operación (FR-001).
///
/// G&amp;T Logística S.A. es un transportista más del padrón, cargado desde la pantalla como
/// cualquier otro y sin trato especial en las reglas de baja ni de asignación (FR-004). Es lo que
/// permite distinguir a los choferes propios de los terciarizados.
///
/// El padrón <b>arranca vacío</b>: no se siembra por migración.
/// </summary>
public class Transportista
{
    public int Id { get; set; }

    /// <summary>Nombre o razón social.</summary>
    public required string Nombre { get; set; }

    /// <summary>
    /// Sólo dígitos, once, con dígito verificador válido (FR-003, FR-025). Único en todo el padrón,
    /// garantizado con un índice en la base y no sólo con la validación previa.
    /// </summary>
    public required string Cuit { get; set; }

    public required TipoPersona Tipo { get; set; }

    public required string Telefono { get; set; }

    /// <summary>Con formato válido, pero <b>sin</b> restricción de unicidad.</summary>
    public required string Email { get; set; }

    /// <summary>
    /// <c>false</c> es la baja lógica: el registro no se borra nunca y el transportista deja de
    /// ofrecerse al registrar o reasignar choferes (FR-001).
    /// </summary>
    public bool Activo { get; set; } = true;

    /// <summary>
    /// Choferes que dependen de este transportista. La baja se rechaza si hay al menos uno
    /// <b>activo</b>, y procede si están todos inactivos o no hay ninguno (FR-010).
    /// </summary>
    public ICollection<Chofer> Choferes { get; } = [];
}
