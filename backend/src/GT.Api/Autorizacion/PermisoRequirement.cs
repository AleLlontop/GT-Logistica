using GT.Api.Autenticacion;
using Microsoft.AspNetCore.Authorization;

namespace GT.Api.Autorizacion;

/// <summary>
/// Exige un permiso concreto, no un rol (FR-006, research §7).
///
/// Evaluar por permiso es lo que dice FR-006 al pie de la letra —"la unión de los permisos de todos
/// sus roles vigentes"— y evita tener que tocar cada endpoint el día que el Módulo 2 cambie qué rol
/// otorga qué permiso.
/// </summary>
public class PermisoRequirement(string codigoPermiso) : IAuthorizationRequirement
{
    public string CodigoPermiso { get; } = codigoPermiso;
}

public class PermisoHandler : AuthorizationHandler<PermisoRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext contexto,
        PermisoRequirement requisito)
    {
        // Los permisos del principal los repone la revalidación en cada petición, así que acá
        // siempre se leen los roles vigentes y nunca los del momento del ingreso (FR-006).
        if (ClaimsSesion.Tiene(contexto.User, requisito.CodigoPermiso))
        {
            contexto.Succeed(requisito);
        }

        return Task.CompletedTask;
    }
}

public static class PoliticasAutorizacion
{
    /// <summary>Nombre de política para un permiso. Se usa como <c>RequireAuthorization(Para(...))</c>.</summary>
    public static string Para(string codigoPermiso) => $"permiso:{codigoPermiso}";

    /// <summary>
    /// Nombre de política para <b>varios permisos exigidos a la vez</b> (Módulo 12, FR-015, research §5).
    ///
    /// Cada reporte pide <c>reportes.emitir</c> <b>además</b> del permiso de lectura de la pantalla de
    /// la que sale. ASP.NET Core exige que <b>todos</b> los requirements de una política se satisfagan,
    /// así que la conjunción sale gratis y el <see cref="PermisoHandler"/> del Módulo 1 no cambia una
    /// línea.
    ///
    /// Va como política y no como un <c>if</c> adentro del endpoint: la autorización se evalúa por
    /// permiso en el pipeline y nunca a mano en el handler (convención [004]). Un <c>if</c> ahí es una
    /// regla de autorización que el pipeline no ve.
    /// </summary>
    public static string ParaTodos(params string[] codigosPermiso) =>
        "permiso:" + string.Join('+', codigosPermiso.Order(StringComparer.Ordinal));

    /// <summary>
    /// Registra la política combinada de <paramref name="codigosPermiso"/>. El nombre lo arma
    /// <see cref="ParaTodos"/>, así que registrarla y pedirla usan la misma cadena.
    /// </summary>
    public static void AgregarPoliticaCombinada(
        this AuthorizationOptions opciones,
        params string[] codigosPermiso) =>
        opciones.AddPolicy(ParaTodos(codigosPermiso), politica =>
            politica.AddRequirements(
                [.. codigosPermiso.Select(codigo => new PermisoRequirement(codigo))]));

    public static void AgregarPoliticasDePermisos(
        this AuthorizationOptions opciones,
        params string[] codigosPermiso)
    {
        foreach (var codigo in codigosPermiso)
        {
            opciones.AddPolicy(Para(codigo), politica =>
                politica.AddRequirements(new PermisoRequirement(codigo)));
        }
    }
}
