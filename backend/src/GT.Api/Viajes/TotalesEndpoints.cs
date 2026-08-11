using GT.Api.Autorizacion;
using GT.Application.Viajes;
using GT.Domain.Usuarios;

namespace GT.Api.Viajes;

/// <summary>
/// Los dos cuadros del período (FR-046, FR-046a, FR-047).
///
/// Va bajo <c>viajes.consultar</c>: es la pantalla que Administración le arma a Gerencia, y ninguno
/// de los dos roles necesita poder tocar nada para verla (FR-051).
///
/// <b>La ruta literal <c>totales</c> convive con <c>/api/viajes/{id:int}</c></b>, declarada con la
/// restricción de tipo en <see cref="ViajesEndpoints"/>. Sin ella el enrutador trataría
/// <c>totales</c> como un identificador y esta ruta quedaría inalcanzable (tasks §trampa 1).
/// </summary>
public static class TotalesEndpoints
{
    public static void MapearTotales(this IEndpointRouteBuilder rutas)
    {
        rutas
            .MapGet("/api/viajes/totales", ConsultarAsync)
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.ViajesConsultar));
    }

    /// <param name="desde">
    /// Obligatorio junto con <paramref name="hasta"/>. Se declaran anulables para poder responder el
    /// rechazo de negocio —<c>rango_de_fechas_requerido</c>, con su mensaje— en vez de fallar al
    /// enlazar con un error técnico (convención [003], FR-046a).
    /// </param>
    private static async Task<IResult> ConsultarAsync(
        DateOnly? desde,
        DateOnly? hasta,
        ConsultarTotales consultar,
        CancellationToken cancelacion)
    {
        var resultado = await consultar.EjecutarAsync(desde, hasta, cancelacion);

        return resultado.Exitoso
            ? Results.Ok(resultado.Totales)
            : RespuestasDeViaje.TraducirFallo(new ResultadoViaje(resultado.Error));
    }
}
