using GT.Domain.Usuarios;

namespace GT.Application.Autenticacion;

/// <summary>Credenciales que llegan del formulario de ingreso.</summary>
public record CredencialesRequest(string? Username, string? Password);

public record RolDto(string Codigo, string Nombre);

public record OpcionMenuDto(string Codigo, string Etiqueta, string Ruta);

/// <summary>
/// Estado de la sesión, tal como lo consume la pantalla de inicio (FR-020).
/// Nunca incluye la contraseña ni su hash (FR-018).
/// </summary>
public record SesionResponse(
    string Username,
    IReadOnlyList<RolDto> Roles,
    IReadOnlyList<OpcionMenuDto> OpcionesMenu)
{
    public static SesionResponse Desde(Usuario usuario) => new(
        usuario.Username,
        [.. usuario.Roles.Select(rol => new RolDto(rol.Codigo, rol.Nombre))],
        [.. CatalogoOpcionesMenu.Autorizadas(usuario.PermisosEfectivos)]);
}
