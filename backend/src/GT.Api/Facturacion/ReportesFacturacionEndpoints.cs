using GT.Api.Autorizacion;
using GT.Application.Facturacion;
using GT.Domain.Usuarios;

namespace GT.Api.Facturacion;

/// <summary>
/// Los dos reportes del módulo: el panel de vencimientos y los totales por cliente (FR-061, FR-063).
///
/// <b>Las dos rutas son literales y conviven con <c>/api/facturas/{id:int}</c></b>. La restricción de tipo
/// la lleva la ruta de identificador, en <c>FacturasEndpoints</c>; sin ella éstas quedarían inalcanzables
/// y <b>no falla ni al compilar ni al arrancar</b>: falla al pedirlas (convención [005], research §15.1).
///
/// Las dos van con <c>facturacion.consultar</c>: se miran sin poder tocar nada.
/// </summary>
public static class ReportesFacturacionEndpoints
{
    public static void MapearReportesDeFacturacion(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/facturas")
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.FacturacionConsultar));

        grupo.MapGet("/vencimientos", ConsultarVencimientosAsync);
        grupo.MapGet("/totales", ConsultarTotalesAsync);
    }

    /// <summary>
    /// Una lista vacía significa que no hay nada vencido ni por vencer, y la pantalla lo dice
    /// explícitamente en vez de mostrar una tabla vacía (FR-063).
    /// </summary>
    private static async Task<IResult> ConsultarVencimientosAsync(
        ConsultarVencimientos consultar,
        CancellationToken cancelacion) =>
        Results.Ok(await consultar.EjecutarAsync(cancelacion));

    /// <summary>
    /// <b>El rango es obligatorio</b>: sin él se responde <c>400 rango_de_fechas_requerido</c> para que la
    /// pantalla diga que falta elegirlo, en vez de mostrar un cuadro vacío que se lee como "no hay
    /// facturas" (FR-061).
    /// </summary>
    private static async Task<IResult> ConsultarTotalesAsync(
        DateOnly? desde,
        DateOnly? hasta,
        ConsultarTotalesFacturacion consultar,
        CancellationToken cancelacion)
    {
        var (rechazo, totales) = await consultar.EjecutarAsync(desde, hasta, cancelacion);

        return rechazo is not null
            ? RespuestasDeFactura.TraducirFallo(rechazo)
            : Results.Ok(totales);
    }
}
