using GT.Api.Autorizacion;
using GT.Application.Usuarios;
using GT.Domain.Usuarios;

namespace GT.Api.Usuarios;

/// <summary>
/// Catálogo de roles y permisos, en modo lectura (FR-010).
///
/// Este módulo <b>no</b> crea, edita ni elimina roles ni permisos: sólo los muestra para que se
/// entienda qué habilita cada rol. Por eso el grupo tiene un único GET y ningún verbo de escritura.
/// </summary>
public static class RolesEndpoints
{
    public static void MapearRoles(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/roles")
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.UsuariosGestionar));

        grupo.MapGet("/", ListarAsync);
    }

    private static async Task<IResult> ListarAsync(
        ConsultarRoles consultar,
        CancellationToken cancelacion)
    {
        var roles = await consultar.EjecutarAsync(cancelacion);

        return Results.Ok(roles);
    }
}
