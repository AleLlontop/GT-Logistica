using GT.Api.Autorizacion;
using GT.Application.Liquidaciones;
using GT.Domain.Usuarios;

namespace GT.Api.Liquidaciones;

/// <summary>
/// Lo que alimenta la generación y el filtro del listado: los transportistas externos y los viajes
/// disponibles (FR-001 a FR-010).
///
/// <b>Las dos rutas son literales y conviven con <c>/api/liquidaciones/{id:int}</c></b>. La restricción de
/// tipo la lleva la ruta de identificador; sin ella estas dos quedarían inalcanzables y <b>no falla ni al
/// compilar ni al arrancar</b>: falla al pedirlas (convención [005], research §12.3).
/// </summary>
public static class ArmadoLiquidacionEndpoints
{
    public static void MapearArmadoDeLiquidaciones(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/liquidaciones");

        // Sólo `consultar`: la usa también el filtro del listado, que Gerencia ve (research §10).
        grupo.MapGet("/transportistas", ConsultarTransportistasAsync)
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.LiquidacionesConsultar));

        grupo.MapGet("/disponibles", ConsultarDisponiblesAsync)
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.LiquidacionesGestionar));
    }

    /// <param name="incluirInactivos">
    /// <c>bool?</c> con <c>?? false</c>: como <c>bool</c> a secas, pedir sin el parámetro fallaría al
    /// enlazar (convención [003]).
    /// </param>
    private static async Task<IResult> ConsultarTransportistasAsync(
        bool? incluirInactivos,
        ConsultarTransportistasLiquidables consultar,
        CancellationToken cancelacion) =>
        Results.Ok(await consultar.EjecutarAsync(incluirInactivos ?? false, cancelacion));

    /// <summary>Una lista vacía es una respuesta legítima: la pantalla la explica (FR-009).</summary>
    private static async Task<IResult> ConsultarDisponiblesAsync(
        int? transportistaId,
        int? mes,
        int? anio,
        ConsultarViajesDisponibles consultar,
        CancellationToken cancelacion)
    {
        var (rechazo, viajes) = await consultar.EjecutarAsync(transportistaId, mes, anio, cancelacion);

        return rechazo is null ? Results.Ok(viajes) : RespuestasDeLiquidacion.TraducirFallo(rechazo);
    }
}
