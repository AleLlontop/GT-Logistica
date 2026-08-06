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
