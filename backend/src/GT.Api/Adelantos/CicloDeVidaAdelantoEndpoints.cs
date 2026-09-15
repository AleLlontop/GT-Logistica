using System.Security.Claims;
using GT.Api.Autenticacion;
using GT.Api.Autorizacion;
using GT.Application.Adelantos;
using GT.Domain.Usuarios;

namespace GT.Api.Adelantos;

/// <summary>
/// Aprobación, rechazo y anulación: cada cambio de estado es un recurso propio (convención [004]), y no hay
/// <c>PUT</c> porque un adelanto no se modifica (FR-035). Los tres responden <c>200</c> con el detalle
/// releído.
/// </summary>
public static class CicloDeVidaAdelantoEndpoints
{
    public static void MapearCicloDeVidaDeAdelantos(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/adelantos")
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.AdelantosGestionar));

        grupo.MapPost("/{id:int}/aprobacion", AprobarAsync);
        grupo.MapPost("/{id:int}/rechazo", RechazarAsync);
        grupo.MapPost("/{id:int}/anulacion", AnularAsync);
    }

    /// <summary>Sin cuerpo y sin confirmación: se deshace con la anulación (research §4).</summary>
    private static async Task<IResult> AprobarAsync(
        int id,
        ClaimsPrincipal principal,
        AprobarAdelanto aprobar,
        CancellationToken cancelacion)
    {
        if (ClaimsSesion.ObtenerIdUsuario(principal) is not { } usuarioId)
        {
            return Results.Unauthorized();
        }

        return Responder(await aprobar.EjecutarAsync(id, usuarioId, cancelacion));
    }

    private static async Task<IResult> RechazarAsync(
        int id,
        RechazoRequest? peticion,
        ClaimsPrincipal principal,
        RechazarAdelanto rechazar,
        CancellationToken cancelacion)
    {
        if (ClaimsSesion.ObtenerIdUsuario(principal) is not { } usuarioId)
        {
            return Results.Unauthorized();
        }

        return Responder(await rechazar.EjecutarAsync(id, peticion, usuarioId, cancelacion));
    }

    private static async Task<IResult> AnularAsync(
        int id,
        AnulacionAdelantoRequest? peticion,
        ClaimsPrincipal principal,
        AnularAdelanto anular,
        CancellationToken cancelacion)
    {
        if (ClaimsSesion.ObtenerIdUsuario(principal) is not { } usuarioId)
        {
            return Results.Unauthorized();
        }

        return Responder(await anular.EjecutarAsync(id, peticion, usuarioId, cancelacion));
    }

    private static IResult Responder(ResultadoAdelanto resultado) =>
        resultado.Exitoso ? Results.Ok(resultado.Adelanto) : RespuestasDeAdelanto.TraducirFallo(resultado);
}
