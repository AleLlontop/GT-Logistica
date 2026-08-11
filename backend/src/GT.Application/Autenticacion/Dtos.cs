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
/// <param name="Permisos">
/// Los códigos de permiso efectivos del usuario.
///
/// Lo agregó el Módulo 5, que es el primero con <b>dos niveles de acceso dentro de una misma
/// pantalla</b>: quien tiene sólo <c>viajes.consultar</c> llega al listado y a la ficha pero no debe
/// ver el botón de alta, ni el de editar, ni el de asignar, ni los de cambio de estado (FR-052).
///
/// Va por <b>permiso</b> y no por rol, aunque el rol ya viajaba acá: es la convención [004] —la
/// autorización se evalúa por permiso y nunca por rol— y evita que el frontend tenga que saber qué
/// rol otorga qué. La restricción no vive sólo en la pantalla: invocar la acción a mano igual
/// devuelve <c>403</c> (SC-012).
/// </param>
public record SesionResponse(
    string Username,
    IReadOnlyList<RolDto> Roles,
    IReadOnlyList<OpcionMenuDto> OpcionesMenu,
    IReadOnlyList<string> Permisos)
{
    public static SesionResponse Desde(Usuario usuario) => new(
        usuario.Username,
        [.. usuario.Roles.Select(rol => new RolDto(rol.Codigo, rol.Nombre))],
        [.. CatalogoOpcionesMenu.Autorizadas(usuario.PermisosEfectivos)],
        [.. usuario.PermisosEfectivos]);
}
