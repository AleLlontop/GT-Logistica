using System.Security.Claims;
using GT.Domain.Usuarios;

namespace GT.Api.Autenticacion;

/// <summary>
/// Armado de la identidad que viaja en la cookie de sesión.
///
/// Los permisos se guardan como claims propios y no como roles de ASP.NET, porque la autorización
/// se evalúa por permiso y no por rol (FR-006, research §7). Se recalculan en cada petición durante
/// la revalidación, así que lo que quedó guardado al ingresar nunca se usa para decidir.
/// </summary>
public static class ClaimsSesion
{
    public const string TipoPermiso = "gt:permiso";
    public const string TipoRol = "gt:rol";
    public const string EsquemaCookie = "gt.sesion";

    /// <summary>
    /// Versión de la credencial con la que se emitió esta cookie (FR-032 del Módulo 2).
    ///
    /// Guarda <c>PasswordActualizadaEn</c> tal cual. La revalidación lo compara contra el valor de
    /// la base y corta la sesión si difieren: es lo que hace que restablecer una contraseña expulse
    /// a quien la tenía abierta.
    /// </summary>
    public const string TipoVersionPassword = "gt:password_v";

    public static ClaimsPrincipal Construir(Usuario usuario)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(ClaimTypes.Name, usuario.Username),
            new(TipoVersionPassword, VersionDePassword(usuario)),
        };

        claims.AddRange(usuario.Roles.Select(rol => new Claim(TipoRol, rol.Codigo)));
        claims.AddRange(usuario.PermisosEfectivos.Select(codigo => new Claim(TipoPermiso, codigo)));

        return new ClaimsPrincipal(new ClaimsIdentity(claims, EsquemaCookie));
    }

    public static int? ObtenerIdUsuario(ClaimsPrincipal principal)
    {
        var valor = principal.FindFirstValue(ClaimTypes.NameIdentifier);

        return int.TryParse(valor, out var id) ? id : null;
    }

    public static bool Tiene(ClaimsPrincipal principal, string codigoPermiso) =>
        principal.HasClaim(TipoPermiso, codigoPermiso);

    /// <summary>Marca de la credencial vigente del usuario, en un formato estable y exacto.</summary>
    public static string VersionDePassword(Usuario usuario) =>
        usuario.PasswordActualizadaEn.Ticks.ToString();

    /// <summary>
    /// Marca con la que se emitió la cookie, o <c>null</c> si no la trae.
    ///
    /// Una cookie sin la marca es una emitida antes de que existiera este mecanismo. Quien compara
    /// decide qué hacer con eso.
    /// </summary>
    public static string? ObtenerVersionDePassword(ClaimsPrincipal principal) =>
        principal.FindFirstValue(TipoVersionPassword);
}
