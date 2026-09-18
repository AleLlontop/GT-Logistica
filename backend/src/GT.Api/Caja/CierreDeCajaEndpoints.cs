using System.Security.Claims;
using GT.Api.Autenticacion;
using GT.Api.Autorizacion;
using GT.Application.Caja;
using GT.Domain.Usuarios;

namespace GT.Api.Caja;

/// <summary>
/// El resumen previo al cierre y su confirmación. Los dos exigen gestionar: el resumen sólo lo ve quien puede
/// cerrar, y su <c>403</c> es lo que decide el aviso de pantalla sin permiso (convención [010]).
/// </summary>
public static class CierreDeCajaEndpoints
{
    public static void MapearCierreDeCaja(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/caja")
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.CajaGestionar));

        grupo.MapGet("/{id:int}/cierre", ObtenerResumenAsync);
        grupo.MapPost("/{id:int}/cierre", CerrarAsync);
    }

    private static async Task<IResult> ObtenerResumenAsync(
        int id,
        ClaimsPrincipal principal,
        ConsultarResumenDeCierre consultar,
        CancellationToken cancelacion)
    {
        if (ClaimsSesion.ObtenerIdUsuario(principal) is not { } usuarioId)
        {
            return Results.Unauthorized();
        }

        var resultado = await consultar.EjecutarAsync(id, usuarioId, cancelacion);

        return resultado.Exitoso ? Results.Ok(resultado.Resumen) : RespuestasDeCaja.TraducirFallo(resultado);
    }

    /// <summary><c>200</c> con el detalle releído, ya cerrada.</summary>
    private static async Task<IResult> CerrarAsync(
        int id,
        CierreRequest? peticion,
        ClaimsPrincipal principal,
        CerrarCaja cerrar,
        CancellationToken cancelacion)
    {
        if (ClaimsSesion.ObtenerIdUsuario(principal) is not { } usuarioId)
        {
            return Results.Unauthorized();
        }

        var resultado = await cerrar.EjecutarAsync(id, peticion, usuarioId, cancelacion);

        return resultado.Exitoso ? Results.Ok(resultado.Caja) : RespuestasDeCaja.TraducirFallo(resultado);
    }
}
