using GT.Application.Usuarios.Personas;
using GT.Domain.Personas;
using GT.Domain.Usuarios;

namespace GT.Application.Usuarios;

/// <summary>
/// Rol tal como lo ve el frontend. Es el mismo contrato que ya usa el Módulo 1 en la sesión.
/// </summary>
public record RolResumen(string Codigo, string Nombre)
{
    public static RolResumen Desde(Rol rol) => new(rol.Codigo, rol.Nombre);
}

/// <summary>Permiso en modo lectura, para mostrar qué habilita un rol (FR-010).</summary>
public record PermisoResumen(string Codigo, string Descripcion);

/// <summary>Permisos de un rol agrupados por módulo de negocio (FR-010).</summary>
public record PermisosDeModulo(string Modulo, IReadOnlyList<PermisoResumen> Permisos);

/// <summary>
/// Rol con sus permisos agrupados. La lista puede venir vacía: es lo esperado mientras los módulos
/// que otorgan esos permisos no estén implementados.
/// </summary>
public record RolConPermisos(
    string Codigo,
    string Nombre,
    IReadOnlyList<PermisosDeModulo> PermisosPorModulo);

/// <summary>
/// Fila del listado de usuarios: exactamente las columnas que exige FR-011.
/// Nunca incluye la contraseña ni su hash (FR-004, FR-013).
/// </summary>
public record UsuarioListado(
    int Id,
    string Username,
    string Email,
    string Estado,
    IReadOnlyList<RolResumen> Roles,
    DateTime FechaAlta,
    DateTime? UltimoAcceso)
{
    public static UsuarioListado Desde(Usuario usuario) => new(
        usuario.Id,
        usuario.Username,
        usuario.Email,
        EstadoUsuarioTexto.Desde(usuario.Estado),
        [.. usuario.Roles.Select(RolResumen.Desde)],
        usuario.FechaAlta,
        usuario.UltimoAcceso);
}

/// <summary>
/// Detalle de un usuario: lo del listado más la persona asociada, si tiene una (FR-013).
/// <c>Persona</c> en <c>null</c> es válido y habitual.
/// </summary>
public record UsuarioDetalle(
    int Id,
    string Username,
    string Email,
    string Estado,
    IReadOnlyList<RolResumen> Roles,
    DateTime FechaAlta,
    DateTime? UltimoAcceso,
    PersonaDto? Persona)
{
    public static UsuarioDetalle Desde(Usuario usuario) => new(
        usuario.Id,
        usuario.Username,
        usuario.Email,
        EstadoUsuarioTexto.Desde(usuario.Estado),
        [.. usuario.Roles.Select(RolResumen.Desde)],
        usuario.FechaAlta,
        usuario.UltimoAcceso,
        usuario.Persona is null ? null : PersonaDto.Desde(usuario.Persona));
}

/// <summary>Datos del alta (FR-001 a FR-005, FR-008).</summary>
public record CrearUsuarioRequest(
    string? Username,
    string? Email,
    string? Password,
    string? Estado,
    IReadOnlyList<string>? Roles,
    int? PersonaId);

/// <summary>
/// Datos de la edición. <b>Sin campo de contraseña, a propósito</b>: para cambiarla está el
/// restablecimiento (FR-014).
/// </summary>
public record ModificarUsuarioRequest(
    string? Username,
    string? Email,
    string? Estado,
    int? PersonaId);

/// <summary>Nueva selección de roles, que reemplaza a la anterior por completo (FR-018).</summary>
public record AsignarRolesRequest(IReadOnlyList<string>? Roles);

/// <summary>Cambio de contraseña propia (FR-030).</summary>
public record CambiarPasswordPropiaRequest(string? PasswordActual, string? PasswordNueva);

/// <summary>
/// Resultado de un restablecimiento. <b>Nunca incluye la contraseña generada</b> (FR-009, SC-004).
/// </summary>
/// <param name="Enviado"><c>false</c> si el correo no pudo entregarse (FR-021).</param>
public record RestablecerPasswordResponse(bool Enviado, string Mensaje);

/// <summary>
/// Traducción entre el enum del dominio y el texto del contrato HTTP.
///
/// El contrato usa <c>activo</c>/<c>inactivo</c>/<c>bloqueado</c> en minúsculas; el dominio usa un
/// enum. La conversión vive acá y no dispersa por los casos de uso.
/// </summary>
public static class EstadoUsuarioTexto
{
    public const string Activo = "activo";
    public const string Inactivo = "inactivo";
    public const string Bloqueado = "bloqueado";

    public static string Desde(EstadoUsuario estado) => estado switch
    {
        EstadoUsuario.Activo => Activo,
        EstadoUsuario.Inactivo => Inactivo,
        EstadoUsuario.Bloqueado => Bloqueado,
        _ => throw new ArgumentOutOfRangeException(nameof(estado)),
    };

    /// <returns><c>null</c> si el texto no corresponde a ninguno de los tres estados.</returns>
    public static EstadoUsuario? Interpretar(string? texto) => texto?.Trim().ToLowerInvariant() switch
    {
        Activo => EstadoUsuario.Activo,
        Inactivo => EstadoUsuario.Inactivo,
        Bloqueado => EstadoUsuario.Bloqueado,
        _ => null,
    };
}

/// <summary>Traducción entre <see cref="TipoIntegrante"/> y el texto del contrato HTTP.</summary>
public static class TipoIntegranteTexto
{
    public const string Chofer = "chofer";
    public const string Empleado = "empleado";

    public static string Desde(TipoIntegrante tipo) => tipo switch
    {
        TipoIntegrante.Chofer => Chofer,
        TipoIntegrante.Empleado => Empleado,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo)),
    };

    /// <returns><c>null</c> si el texto no corresponde a ninguno de los dos tipos.</returns>
    public static TipoIntegrante? Interpretar(string? texto) => texto?.Trim().ToLowerInvariant() switch
    {
        Chofer => TipoIntegrante.Chofer,
        Empleado => TipoIntegrante.Empleado,
        _ => null,
    };
}
