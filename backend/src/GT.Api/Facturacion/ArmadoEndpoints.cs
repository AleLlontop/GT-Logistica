using GT.Api.Autorizacion;
using GT.Application.Facturacion;
using GT.Domain.Usuarios;

namespace GT.Api.Facturacion;

/// <summary>
/// Lo que alimenta la pantalla de alta: los viajes facturables, las anuladas sin reemplazo y la vista
/// previa del documento (FR-015 a FR-021, FR-033, FR-049).
///
/// <b>Las tres rutas son literales y conviven con <c>/api/facturas/{id:int}</c></b>. La restricción de
/// tipo la lleva la ruta de identificador, en <c>FacturasEndpoints</c>; sin ella estas tres quedarían
/// inalcanzables y <b>no falla ni al compilar ni al arrancar</b>: falla al pedirlas (convención [005],
/// research §15.1).
///
/// Las tres exigen <c>facturacion.gestionar</c> y no <c>consultar</c>: no son pantallas de lectura, son
/// insumos del alta.
/// </summary>
public static class ArmadoEndpoints
{
    public static void MapearArmadoDeFacturas(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/facturas")
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.FacturacionGestionar));

        grupo.MapGet("/facturables", ConsultarFacturablesAsync);
        grupo.MapGet("/anuladas-sin-reemplazo", ConsultarAnuladasSinReemplazoAsync);
        grupo.MapPost("/vista-previa", VistaPreviaAsync);
    }

    /// <summary>
    /// Una lista vacía es una respuesta legítima y la pantalla la explica nombrando la combinación de
    /// cliente, mes y año (FR-021). Los viajes sin remito vienen marcados, no escondidos (FR-019a).
    /// </summary>
    private static async Task<IResult> ConsultarFacturablesAsync(
        int clienteId,
        int mes,
        int anio,
        ConsultarFacturables consultar,
        CancellationToken cancelacion)
    {
        if (clienteId <= 0 || mes is < 1 or > 12 || !PreparadorDeFactura.AniosValidos.Contains(anio))
        {
            return RespuestasDeFactura.TraducirFallo(
                new ResultadoFactura(ErrorFactura.DatosInvalidos));
        }

        return Results.Ok(await consultar.EjecutarAsync(clienteId, mes, anio, cancelacion));
    }

    private static async Task<IResult> ConsultarAnuladasSinReemplazoAsync(
        int clienteId,
        ConsultarAnuladasSinReemplazo consultar,
        CancellationToken cancelacion) =>
        Results.Ok(await consultar.EjecutarAsync(clienteId, cancelacion));

    /// <summary>
    /// Devuelve <b>el documento en PDF</b>, armado por el mismo armador que produce el archivo al emitir
    /// (FR-033). No crea la factura, no guarda ningún archivo y no registra nada.
    ///
    /// Es <c>POST</c> y no <c>GET</c> porque lleva la selección de viajes en el cuerpo. Se sirve en
    /// línea y la pantalla lo muestra en un marco sobre una URL de <c>Blob</c>.
    /// </summary>
    private static async Task<IResult> VistaPreviaAsync(
        EmisionRequest peticion,
        VistaPreviaFactura vistaPrevia,
        HttpContext contexto,
        CancellationToken cancelacion)
    {
        var (rechazo, pdf) = await vistaPrevia.EjecutarAsync(peticion, cancelacion);

        if (rechazo is not null)
        {
            return RespuestasDeFactura.TraducirFallo(rechazo);
        }

        // `nosniff` igual que al servir el documento guardado: es contenido en línea desde el propio
        // origen, y sin la cabecera el navegador puede ignorar el tipo declarado (convención [003]).
        contexto.Response.Headers.XContentTypeOptions = "nosniff";

        return Results.File(pdf!.Contenido, "application/pdf");
    }
}
