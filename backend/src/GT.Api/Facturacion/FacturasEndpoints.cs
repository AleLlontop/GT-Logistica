using System.Security.Claims;
using GT.Api.Archivos;
using GT.Api.Autenticacion;
using GT.Api.Autorizacion;
using GT.Application.Facturacion;
using GT.Domain.Usuarios;

namespace GT.Api.Facturacion;

/// <summary>
/// Listado, ficha, emisión, corrección y documento de las facturas.
///
/// <b>Las cuatro rutas con identificador llevan <c>{id:int}</c>, y no es decorativo</b>: en este mismo
/// prefijo conviven cinco rutas literales —<c>facturables</c>, <c>vista-previa</c>,
/// <c>anuladas-sin-reemplazo</c>, <c>vencimientos</c> y <c>totales</c>—. Sin la restricción,
/// <c>/api/facturas/{id}</c> las capturaría a todas y las cinco quedarían inalcanzables: el enrutador
/// las trataría como identificadores y fallaría al convertirlas. <b>No falla al compilar ni al
/// arrancar</b>, falla al pedirlas (convención [005], research §15.1).
///
/// Los <c>GET</c> exigen <c>facturacion.consultar</c> y las escrituras <c>facturacion.gestionar</c>
/// (FR-067). La anulación tiene su permiso propio y vive en <c>CicloDeVidaFacturaEndpoints</c>.
/// </summary>
public static class FacturasEndpoints
{
    public static void MapearFacturas(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/facturas");

        var consultar = PoliticasAutorizacion.Para(CodigosPermiso.FacturacionConsultar);
        var gestionar = PoliticasAutorizacion.Para(CodigosPermiso.FacturacionGestionar);

        grupo.MapGet("/", ListarAsync).RequireAuthorization(consultar);
        grupo.MapGet("/{id:int}", ObtenerAsync).RequireAuthorization(consultar);
        grupo.MapGet("/{id:int}/documento", ServirDocumentoAsync).RequireAuthorization(consultar);

        grupo.MapPost("/", EmitirAsync).RequireAuthorization(gestionar);
        grupo.MapPut("/{id:int}", CorregirAsync).RequireAuthorization(gestionar);
    }

    /// <param name="estado">
    /// Opera sobre el estado <b>derivado</b> y sus cuatro valores son excluyentes (FR-058a). Omitirlo
    /// devuelve <b>todas, incluidas las anuladas</b> —al revés que el listado de viajes— y el control de
    /// la pantalla lo dice explícitamente (FR-064). Un valor desconocido se ignora en vez de romper:
    /// filtrar de más no es un error (convención [003]).
    /// </param>
    /// <param name="pagina">
    /// Anulable a propósito: pedir el listado sin el parámetro tiene que tomar el valor por defecto en
    /// vez de fallar al enlazar (convención [003]).
    /// </param>
    private static async Task<IResult> ListarAsync(
        int? clienteId,
        DateOnly? desde,
        DateOnly? hasta,
        int? mes,
        int? anio,
        string? estado,
        string? tipoComprobante,
        int? pagina,
        ConsultarFacturas consultar,
        CancellationToken cancelacion)
    {
        var filtros = new FiltrosDeFacturas(
            clienteId,
            desde,
            hasta,
            mes,
            anio,
            NombresDeEstadoFactura.LeerEstado(estado),
            NombresDeEstadoFactura.LeerTipoComprobante(tipoComprobante),
            pagina ?? 1);

        return Results.Ok(await consultar.EjecutarAsync(filtros, cancelacion));
    }

    private static async Task<IResult> ObtenerAsync(
        int id,
        ConsultarFichaFactura consultar,
        CancellationToken cancelacion)
    {
        var factura = await consultar.EjecutarAsync(id, cancelacion);

        return factura is not null ? Results.Ok(factura) : RespuestasDeFactura.NoEncontrada();
    }

    /// <summary>
    /// El documento, <b>en línea</b> y con un nombre que identifica la factura (FR-031a).
    ///
    /// Disponible en <b>cualquier estado</b> (FR-031d). Si la factura está anulada, el documento ya trae
    /// impresas la leyenda y el motivo porque se regeneró al anularla: la leyenda <b>no</b> se estampa
    /// acá — el documento se arma en un solo lugar.
    /// </summary>
    private static async Task<IResult> ServirDocumentoAsync(
        int id,
        ServirDocumentoFactura servir,
        HttpContext contexto,
        CancellationToken cancelacion)
    {
        var archivo = await servir.EjecutarAsync(id, cancelacion);

        // 404 también cuando la factura existe pero su archivo ya no está en el volumen: son la misma
        // respuesta para quien consulta.
        return archivo is null
            ? RespuestasDeFactura.NoEncontrada()
            : ResultadoArchivo.EnLinea(
                contexto,
                archivo.Contenido,
                archivo.TipoContenido,
                archivo.Nombre);
    }

    /// <summary>
    /// Emite la factura (FR-014, FR-054).
    ///
    /// <b>El cuerpo no lleva <c>neto</c>, <c>iva</c> ni <c>total</c></b>: no están en
    /// <see cref="EmisionRequest"/>, así que no hay nada que ignorar (FR-024).
    ///
    /// El usuario de la sesión se lee acá y viaja <b>por parámetro</b> al caso de uso, igual que en el
    /// Módulo 5: no se introduce una abstracción de usuario actual que hoy tendría cinco llamadores.
    /// </summary>
    private static async Task<IResult> EmitirAsync(
        EmisionRequest peticion,
        ClaimsPrincipal principal,
        EmitirFactura emitir,
        CancellationToken cancelacion)
    {
        if (ClaimsSesion.ObtenerIdUsuario(principal) is not { } usuarioId)
        {
            return Results.Unauthorized();
        }

        var resultado = await emitir.EjecutarAsync(peticion, usuarioId, cancelacion);

        return resultado.Exitoso
            ? Results.Created($"/api/facturas/{resultado.Factura!.Id}", resultado.Factura)
            : RespuestasDeFactura.TraducirFallo(resultado);
    }

    /// <summary>
    /// Corrige los cuatro campos de FR-035 y <b>ningún otro</b>: el cliente, los viajes y los importes
    /// no están en <see cref="CorreccionRequest"/>, y tampoco el estado ni la fecha de cobro, que
    /// tienen su recurso propio (FR-036, FR-044, research §15.5).
    /// </summary>
    private static async Task<IResult> CorregirAsync(
        int id,
        CorreccionRequest peticion,
        ClaimsPrincipal principal,
        CorregirFactura corregir,
        CancellationToken cancelacion)
    {
        if (ClaimsSesion.ObtenerIdUsuario(principal) is not { } usuarioId)
        {
            return Results.Unauthorized();
        }

        var resultado = await corregir.EjecutarAsync(id, peticion, usuarioId, cancelacion);

        return resultado.Exitoso
            ? Results.Ok(resultado.Factura)
            : RespuestasDeFactura.TraducirFallo(resultado);
    }
}
