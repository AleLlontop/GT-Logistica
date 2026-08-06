namespace GT.Domain.Usuarios;

/// <summary>
/// Autorización concreta sobre una funcionalidad del sistema, agrupada por módulo de negocio y
/// otorgada a través de uno o más roles. El catálogo queda cargado en la instalación (FR-019) y
/// ningún módulo lo edita en esta versión.
/// </summary>
public class Permiso
{
    public int Id { get; set; }

    /// <summary>Identificador estable con formato <c>modulo.accion</c>, por ejemplo <c>usuarios.gestionar</c>.</summary>
    public required string Codigo { get; set; }

    /// <summary>Módulo de negocio al que pertenece, para poder agruparlos.</summary>
    public required string Modulo { get; set; }

    public required string Descripcion { get; set; }

    public ICollection<Rol> Roles { get; set; } = [];
}
