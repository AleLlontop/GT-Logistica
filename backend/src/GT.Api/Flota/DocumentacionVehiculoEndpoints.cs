using GT.Api.Archivos;
using GT.Api.Autorizacion;
using GT.Application.Autenticacion;
using GT.Application.Choferes.Documentacion;
using GT.Application.Flota;
using GT.Application.Flota.Documentacion;
using GT.Domain.Usuarios;

namespace GT.Api.Flota;

/// <summary>
/// Documentación de los vehículos: carga, corrección, eliminación, descarga del escaneo y el panel de
/// vencimientos.
///
/// Va como <c>multipart/form-data</c> porque puede traer el archivo adjunto, que es opcional
/// (FR-016a). <b>Ningún cuerpo acepta un campo de estado</b>: lo calcula el sistema y no se recibe
/// por ninguna vía (FR-021, SC-004).
///
/// La descarga exige el mismo permiso que el resto, incluso siendo una lectura de archivo: el adjunto
/// se sirve por este endpoint y nunca por una URL pública, así que conocer la ruta no alcanza
/// (FR-038, SC-011).
///
/// <b>Las rutas llevan el prefijo <c>/api/flota/</c></b> porque el Módulo 3 ya ocupó
/// <c>/api/documentacion/{id}</c> con los documentos del chofer: sin prefijo, dos entidades distintas
/// compartirían espacio de identificadores (research §12).
/// </summary>
public static class DocumentacionVehiculoEndpoints
{
    public static void MapearDocumentacionVehiculo(this IEndpointRouteBuilder rutas)
    {
        var politica = PoliticasAutorizacion.Para(CodigosPermiso.FlotaGestionar);

        rutas.MapPost("/api/flota/vehiculos/{vehiculoId:int}/documentacion", CargarAsync)
            .RequireAuthorization(politica)
            .DisableAntiforgery();

        var documentacion = rutas
            .MapGroup("/api/flota/documentacion")
            .RequireAuthorization(politica);

        documentacion.MapPut("/{id:int}", CorregirAsync).DisableAntiforgery();
        documentacion.MapDelete("/{id:int}", EliminarAsync);
        documentacion.MapGet("/{id:int}/archivo", DescargarAsync);

        // El panel vive fuera del grupo porque cuelga de /api/flota/vencimientos.
        rutas.MapGet("/api/flota/vencimientos", ConsultarVencimientosAsync)
            .RequireAuthorization(politica);
    }

    /// <summary>
    /// Una lista vacía significa que no hay vencimientos pendientes, y la pantalla lo dice
    /// explícitamente en vez de mostrar una tabla vacía (FR-036, US5 esc. 5).
    /// </summary>
    private static async Task<IResult> ConsultarVencimientosAsync(
        ConsultarVencimientosFlota consultar,
        CancellationToken cancelacion) =>
        Results.Ok(await consultar.EjecutarAsync(cancelacion));

    private static async Task<IResult> CargarAsync(
        int vehiculoId,
        HttpRequest peticion,
        CargarDocumentoVehiculo cargar,
        CancellationToken cancelacion)
    {
        if (!peticion.HasFormContentType)
        {
            return DatosInvalidos();
        }

        var formulario = await peticion.ReadFormAsync(cancelacion);
        var resultado = await cargar.EjecutarAsync(
            vehiculoId,
            LeerDocumento(formulario),
            LeerArchivo(formulario),
            cancelacion);

        if (resultado.Exitoso)
        {
            return Results.Created(
                $"/api/flota/documentacion/{resultado.Documento!.Id}",
                resultado.Documento);
        }

        return TraducirFallo(resultado);
    }

    private static async Task<IResult> CorregirAsync(
        int id,
        HttpRequest peticion,
        CorregirDocumentoVehiculo corregir,
        CancellationToken cancelacion)
    {
        if (!peticion.HasFormContentType)
        {
            return DatosInvalidos();
        }

        var formulario = await peticion.ReadFormAsync(cancelacion);
        var resultado = await corregir.EjecutarAsync(
            id,
            LeerDocumento(formulario),
            LeerArchivo(formulario),
            cancelacion);

        return resultado.Exitoso ? Results.Ok(resultado.Documento) : TraducirFallo(resultado);
    }

    private static async Task<IResult> EliminarAsync(
        int id,
        EliminarDocumentoVehiculo eliminar,
        CancellationToken cancelacion)
    {
        var resultado = await eliminar.EjecutarAsync(id, cancelacion);

        return resultado.Exitoso ? Results.NoContent() : TraducirFallo(resultado);
    }

    private static async Task<IResult> DescargarAsync(
        int id,
        DescargarArchivoDocumentoVehiculo descargar,
        HttpContext contexto,
        CancellationToken cancelacion)
    {
        var archivo = await descargar.EjecutarAsync(id, cancelacion);

        // 404 también cuando el documento existe pero no tiene archivo: son la misma respuesta para
        // quien consulta (contracts/flota-api.yaml).
        //
        // En línea y no como descarga: quien abre un documento lo quiere ver, no bajarlo primero.
        return archivo is null
            ? NoEncontrado()
            : ResultadoArchivo.EnLinea(
                contexto,
                archivo.Contenido,
                archivo.TipoContenido,
                archivo.Nombre);
    }

    private static DocumentoRequest LeerDocumento(IFormCollection formulario) => new(
        int.TryParse(formulario["documentacionTipoId"], out var tipoId) ? tipoId : null,
        formulario["numero"],
        formulario["fechaEmision"],
        formulario["fechaVencimiento"]);

    /// <summary>
    /// El adjunto, o <c>null</c> si no vino ninguno. Un documento sin escaneo es válido: queda como
    /// documentación sin respaldo, que el sistema distingue de una respaldada y que no altera el
    /// estado del vehículo (FR-016a).
    /// </summary>
    private static ArchivoCargado? LeerArchivo(IFormCollection formulario)
    {
        var archivo = formulario.Files["archivo"];

        if (archivo is null || archivo.Length == 0)
        {
            return null;
        }

        return new ArchivoCargado(
            Path.GetFileName(archivo.FileName),
            archivo.Length,
            archivo.OpenReadStream);
    }

    private static IResult NoEncontrado() => Results.NotFound(
        new ErrorResponse(CodigosErrorFlota.NoEncontrado, MensajesFlota.NoEncontrado));

    private static IResult DatosInvalidos() => Results.BadRequest(
        new ErrorResponse(CodigosErrorFlota.DatosInvalidos, MensajesFlota.DatosInvalidos));

    private static IResult TraducirFallo(ResultadoDocumentoVehiculo resultado) => resultado.Error switch
    {
        ErrorDocumentoVehiculo.NoEncontrado or
            ErrorDocumentoVehiculo.VehiculoNoEncontrado => NoEncontrado(),

        ErrorDocumentoVehiculo.TipoInexistente => Results.BadRequest(new ErrorResponse(
            CodigosErrorFlota.TipoInexistente,
            MensajesFlota.TipoInexistente,
            resultado.Campo)),

        ErrorDocumentoVehiculo.VencimientoAnteriorAEmision => Results.BadRequest(new ErrorResponse(
            CodigosErrorFlota.VencimientoAnteriorAEmision,
            MensajesFlota.VencimientoAnteriorAEmision,
            resultado.Campo)),

        ErrorDocumentoVehiculo.ArchivoNoAdmitido => Results.BadRequest(new ErrorResponse(
            CodigosErrorFlota.ArchivoNoAdmitido,
            MensajesFlota.ArchivoNoAdmitido,
            resultado.Campo)),

        // 500 y no 400: el archivo era válido y el problema fue del sistema, no de lo que cargaron.
        // El documento no se creó ni se modificó, y la pantalla conserva lo tipeado (FR-029).
        ErrorDocumentoVehiculo.ArchivoNoGuardado => Results.Json(
            new ErrorResponse(
                CodigosErrorFlota.ArchivoNoGuardado,
                MensajesFlota.ArchivoNoGuardado,
                resultado.Campo),
            statusCode: StatusCodes.Status500InternalServerError),

        _ => Results.BadRequest(new ErrorResponse(
            CodigosErrorFlota.DatosInvalidos,
            MensajesFlota.DatosInvalidos,
            resultado.Campo)),
    };
}
