using System.Security.Claims;
using GT.Api.Autenticacion;
using GT.Api.Autorizacion;
using GT.Application.Liquidaciones;
using GT.Domain.Usuarios;

namespace GT.Api.Liquidaciones;

/// <summary>
/// La anulación y las órdenes de pago: cada una es un recurso propio y nunca un campo del <c>PUT</c> de
/// edición (convención [004]). Las dos se confirman en el backend porque no se deshacen (research §7).
/// </summary>
public static class CicloDeVidaLiquidacionEndpoints
{
    public static void MapearCicloDeVidaDeLiquidaciones(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/liquidaciones")
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.LiquidacionesGestionar));

        grupo.MapPost("/{id:int}/anulacion", AnularAsync);
        grupo.MapPost("/{id:int}/ordenes-de-pago", RegistrarOrdenDePagoAsync);
    }

    private static async Task<IResult> AnularAsync(
        int id,
        AnulacionRequest? peticion,
        ClaimsPrincipal principal,
        AnularLiquidacion anular,
        CancellationToken cancelacion)
    {
        if (ClaimsSesion.ObtenerIdUsuario(principal) is not { } usuarioId)
        {
            return Results.Unauthorized();
        }

        var resultado = await anular.EjecutarAsync(id, peticion, usuarioId, cancelacion);

        return resultado.Exitoso
            ? Results.Ok(resultado.Liquidacion)
            : RespuestasDeLiquidacion.TraducirFallo(resultado);
    }

    /// <summary><c>201</c> con el detalle releído de la liquidación.</summary>
    private static async Task<IResult> RegistrarOrdenDePagoAsync(
        int id,
        OrdenDePagoRequest? peticion,
        ClaimsPrincipal principal,
        RegistrarOrdenDePago registrar,
        CancellationToken cancelacion)
    {
        if (ClaimsSesion.ObtenerIdUsuario(principal) is not { } usuarioId)
        {
            return Results.Unauthorized();
        }

        var resultado = await registrar.EjecutarAsync(id, peticion, usuarioId, cancelacion);

        return resultado.Exitoso
            ? Results.Created($"/api/liquidaciones/{id}", resultado.Liquidacion)
            : RespuestasDeLiquidacion.TraducirFallo(resultado);
    }
}
