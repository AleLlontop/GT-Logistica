using System.Security.Claims;
using GT.Domain.Usuarios;
using GT.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace GT.Api.Autenticacion;

/// <summary>
/// Revalidación de la sesión en cada petición.
///
/// Es la pieza que hace que la cookie se comporte como pide la spec y que un token autocontenido no
/// podría cumplir sin infraestructura extra:
///
/// - FR-006: los permisos efectivos se recalculan con los roles vigentes en el momento de cada
///   operación, no con los que el usuario tenía al ingresar.
/// - FR-009: si la cuenta dejó de estar `activa`, la sesión se rechaza en la petición siguiente.
/// - FR-032 (Módulo 2): si la contraseña cambió después de emitirse esta cookie, la sesión se
///   rechaza. Una contraseña que dejó de ser válida no puede seguir sosteniendo una sesión viva.
///
/// Cuesta una consulta por petición. A la escala del sistema —decenas de usuarios— es irrelevante.
/// </summary>
public class RevalidadorSesion(GtDbContext contexto)
{
    public async Task RevalidarAsync(CookieValidatePrincipalContext contexto_)
    {
        var idUsuario = contexto_.Principal is null
            ? null
            : ClaimsSesion.ObtenerIdUsuario(contexto_.Principal);

        if (idUsuario is null)
        {
            await RechazarAsync(contexto_);
            return;
        }

        var usuario = await contexto.Usuarios
            .Include(u => u.Roles)
            .ThenInclude(rol => rol.Permisos)
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == idUsuario);

        if (usuario is null || !usuario.PuedeAutenticarse)
        {
            await RechazarAsync(contexto_);
            return;
        }

        // FR-032: la cookie se emitió con una credencial que ya no es la vigente.
        if (SeEmitioConOtraPassword(contexto_.Principal!, usuario))
        {
            await RechazarAsync(contexto_);
            return;
        }

        // Se reemplaza la identidad con roles y permisos frescos, de modo que ninguna decisión de
        // autorización posterior use lo que quedó guardado en la cookie.
        contexto_.ReplacePrincipal(ClaimsSesion.Construir(usuario));
        contexto_.ShouldRenew = true;
    }

    /// <summary>
    /// Compara la marca de credencial que viaja en la cookie con la vigente en la base (FR-032).
    ///
    /// Es una comparación por igualdad y no por orden temporal, a propósito. La alternativa evidente
    /// —mirar el <c>IssuedUtc</c> de la cookie y ver si es anterior al último cambio de contraseña—
    /// no funciona: <c>IssuedUtc</c> viaja como texto RFC1123, que <b>no tiene fracciones de
    /// segundo</b>, así que todo lo que ocurre dentro del mismo segundo queda indistinguible.
    /// Corregirlo con truncados o tolerancias deja siempre un borde roto: o sobrevive una sesión que
    /// debía morir, o se expulsa al usuario de la sesión desde la que acaba de cambiar su propia
    /// contraseña.
    ///
    /// Con una marca exacta no hay bordes: la cookie que se emitió con la credencial vigente
    /// sobrevive y cualquier otra muere, sin importar cuánto tiempo pasó.
    ///
    /// <c>ShouldRenew</c> vuelve a emitir la cookie en cada petición con la marca fresca, así que
    /// una sesión legítima siempre coincide.
    /// </summary>
    private static bool SeEmitioConOtraPassword(ClaimsPrincipal principal, Usuario usuario)
    {
        var enLaCookie = ClaimsSesion.ObtenerVersionDePassword(principal);

        // Sin marca es una cookie emitida antes de que existiera este mecanismo. Se corta: es una
        // sesión de la que no se puede afirmar que siga respaldada por la contraseña vigente, y el
        // costo de equivocarse es pedir un ingreso de más, no dejar viva una sesión que no debía.
        return enLaCookie is null || enLaCookie != ClaimsSesion.VersionDePassword(usuario);
    }

    private static async Task RechazarAsync(CookieValidatePrincipalContext contexto_)
    {
        contexto_.RejectPrincipal();
        await contexto_.HttpContext.SignOutAsync(ClaimsSesion.EsquemaCookie);
    }
}
