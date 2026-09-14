using System.Security.Claims;
using GT.Api.Autenticacion;
using GT.Api.Autorizacion;
using GT.Application.Liquidaciones;
using GT.Domain.Usuarios;

namespace GT.Api.Liquidaciones;

/// <summary>
/// Listado, detalle, generación y edición.
///
/// <b>Las rutas con identificador llevan <c>{id:int}</c>, y no es decorativo</b>: en el mismo prefijo
/// conviven <c>/transportistas</c> y <c>/disponibles</c> (convención [005]).
///
/// El usuario de la sesión se lee acá y viaja <b>por parámetro</b> al caso de uso, como en los Módulos 5
/// y 6.
/// </summary>
public static class LiquidacionesEndpoints
{
    public static void MapearLiquidaciones(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/liquidaciones");

        var consultar = PoliticasAutorizacion.Para(CodigosPermiso.LiquidacionesConsultar);
        var gestionar = PoliticasAutorizacion.Para(CodigosPermiso.LiquidacionesGestionar);

        grupo.MapGet("/", ListarAsync).RequireAuthorization(consultar);
        grupo.MapGet("/{id:int}", ObtenerAsync).RequireAuthorization(consultar);
        grupo.MapPost("/", GenerarAsync).RequireAuthorization(gestionar);
        grupo.MapPut("/{id:int}", EditarAsync).RequireAuthorization(gestionar);
    }

    /// <param name="estado">
    /// Sin él, <b>todas, incluidas las anuladas</b>. Un valor desconocido se ignora (convención [003]).
    /// </param>
    private static async Task<IResult> ListarAsync(
        int? transportistaId,
        int? mes,
        int? anio,
        string? estado,
        int? pagina,
        ConsultarLiquidaciones consultar,
        CancellationToken cancelacion)
    {
        var filtros = new FiltrosDeLiquidaciones(
            transportistaId,
            mes,
            anio,
            NombresDeEstadoLiquidacion.LeerEstado(estado),
            Math.Max(pagina ?? 1, 1));

        return Results.Ok(await consultar.EjecutarAsync(filtros, cancelacion));
    }

    private static async Task<IResult> ObtenerAsync(
        int id,
        ConsultarDetalleLiquidacion consultar,
        CancellationToken cancelacion)
    {
        var detalle = await consultar.EjecutarAsync(id, cancelacion);

        return detalle is not null ? Results.Ok(detalle) : RespuestasDeLiquidacion.NoEncontrada();
    }

    /// <summary>El cuerpo no lleva importes: no están en <see cref="GeneracionRequest"/> (FR-007).</summary>
    private static async Task<IResult> GenerarAsync(
        GeneracionRequest? peticion,
        ClaimsPrincipal principal,
        GenerarLiquidacion generar,
        CancellationToken cancelacion)
    {
        if (ClaimsSesion.ObtenerIdUsuario(principal) is not { } usuarioId)
        {
            return Results.Unauthorized();
        }

        var resultado = await generar.EjecutarAsync(peticion, usuarioId, cancelacion);

        return resultado.Exitoso
            ? Results.Created($"/api/liquidaciones/{resultado.Liquidacion!.Id}", resultado.Liquidacion)
            : RespuestasDeLiquidacion.TraducirFallo(resultado);
    }

    /// <summary>Recibe el conjunto final de viajes y la versión abierta (FR-048, research §10).</summary>
    private static async Task<IResult> EditarAsync(
        int id,
        EdicionRequest? peticion,
        ClaimsPrincipal principal,
        EditarLiquidacion editar,
        CancellationToken cancelacion)
    {
        if (ClaimsSesion.ObtenerIdUsuario(principal) is not { } usuarioId)
        {
            return Results.Unauthorized();
        }

        var resultado = await editar.EjecutarAsync(id, peticion, usuarioId, cancelacion);

        return resultado.Exitoso
            ? Results.Ok(resultado.Liquidacion)
            : RespuestasDeLiquidacion.TraducirFallo(resultado);
    }
}
