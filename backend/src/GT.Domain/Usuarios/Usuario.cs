using GT.Domain.Personas;

namespace GT.Domain.Usuarios;

/// <summary>
/// Cuenta de acceso al sistema para un integrante del personal de G&amp;T Logística.
///
/// El Módulo 1 la lee para autenticar y escribe <see cref="UltimoAcceso"/>; el Módulo 2 la crea, la
/// edita, le asigna roles y la da de baja lógicamente.
/// </summary>
public class Usuario
{
    public int Id { get; set; }

    /// <summary>Tal como lo escribió quien creó la cuenta. Se usa para mostrarlo, no para buscar.</summary>
    public required string Username { get; set; }

    /// <summary>
    /// Username con espacios recortados y en mayúsculas invariantes. Es el campo por el que se
    /// busca al autenticar, y el que lleva el índice único (FR-012).
    /// </summary>
    public required string UsernameNormalizado { get; set; }

    /// <summary>
    /// Producido por el hasheador. Nunca sale de la base hacia una respuesta ni hacia un log
    /// (FR-002, FR-018).
    /// </summary>
    public required string PasswordHash { get; set; }

    /// <summary>Tal como lo escribió quien creó la cuenta. Se usa para mostrarlo, no para buscar.</summary>
    public required string Email { get; set; }

    /// <summary>
    /// Email con espacios recortados y en minúsculas invariantes. Lleva el índice único que garantiza
    /// FR-003, y es la columna por la que filtra el listado (FR-011, FR-020).
    /// </summary>
    public required string EmailNormalizado { get; set; }

    public EstadoUsuario Estado { get; set; } = EstadoUsuario.Activo;

    /// <summary>Fecha y hora UTC del alta. Se fija una vez y no se edita (FR-011).</summary>
    public DateTime FechaAlta { get; set; }

    /// <summary>Fecha y hora UTC del último ingreso exitoso. <c>null</c> si nunca ingresó.</summary>
    public DateTime? UltimoAcceso { get; set; }

    /// <summary>
    /// <c>null</c> significa que la contraseña es definitiva. Con valor, la contraseña es temporal
    /// y sólo sirve dentro de las 24 horas siguientes a esa marca (FR-017). La escribe el Módulo 2
    /// al restablecer una contraseña; este módulo sólo la lee.
    /// </summary>
    public DateTime? PasswordTemporalGeneradaEn { get; set; }

    /// <summary>
    /// Momento UTC del último cambio de contraseña: lo escriben el alta, el restablecimiento y el
    /// cambio propio.
    ///
    /// Es lo que permite cortar las sesiones abiertas cuando la contraseña deja de ser válida
    /// (FR-032): el revalidador rechaza toda sesión emitida antes de esta marca. Una contraseña que
    /// ya no sirve no puede seguir sosteniendo una sesión viva (research §10).
    /// </summary>
    public DateTime PasswordActualizadaEn { get; set; }

    /// <summary>
    /// Persona asociada, o <c>null</c>. Que no tenga ninguna es válido y habitual (FR-008); por eso
    /// el índice único de esta columna es filtrado.
    /// </summary>
    public int? PersonaId { get; set; }

    public Persona? Persona { get; set; }

    public ICollection<Rol> Roles { get; set; } = [];

    /// <summary>Sólo un usuario en estado <see cref="EstadoUsuario.Activo"/> puede autenticarse (FR-001).</summary>
    public bool PuedeAutenticarse => Estado == EstadoUsuario.Activo;

    /// <summary>
    /// Permisos efectivos: la unión de los permisos de todos sus roles vigentes. Se calcula en cada
    /// operación, no en el momento del ingreso (FR-006).
    /// </summary>
    public IEnumerable<string> PermisosEfectivos =>
        Roles.SelectMany(rol => rol.Permisos).Select(permiso => permiso.Codigo).Distinct();
}
