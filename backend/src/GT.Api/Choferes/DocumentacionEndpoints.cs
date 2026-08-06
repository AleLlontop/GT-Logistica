using GT.Api.Autorizacion;
using GT.Application.Autenticacion;
using GT.Application.Choferes;
using GT.Application.Choferes.Documentacion;
using GT.Domain.Usuarios;

namespace GT.Api.Choferes;

/// <summary>
/// Documentación de los choferes: carga, corrección, eliminación y descarga del escaneo.
///
/// Va como <c>multipart/form-data</c> porque puede traer el archivo adjunto, que es opcional
/// (FR-015). <b>Ningún cuerpo acepta un campo de estado</b>: lo calcula el sistema y no se recibe
/// por ninguna vía (FR-018, SC-004).
///
/// La descarga exige el mismo permiso que el resto, incluso siendo una lectura de archivo: un
/// psicofísico es un dato personal sensible y conocer la ruta no puede alcanzar (FR-024, SC-011).
/// </summary>
public static class DocumentacionEndpoints
{
    public static void MapearDocumentacion(this IEndpointRouteBuilder rutas)
    {
        var politica = PoliticasAutorizacion.Para(CodigosPermiso.ChoferesGestionar);

        rutas.MapPost("/api/choferes/{choferId:int}/documentacion", CargarAsync)
            .RequireAuthorization(politica)
            .DisableAntiforgery();

        var documentacion = rutas
            .MapGroup("/api/documentacion")
            .RequireAuthorization(politica);

        documentacion.MapPut("/{id:int}", CorregirAsync).DisableAntiforgery();
        documentacion.MapDelete("/{id:int}", EliminarAsync);
        documentacion.MapGet("/{id:int}/archivo", DescargarAsync);

        // El panel vive fuera del grupo porque cuelga de /api/vencimientos, no de /api/documentacion.
        rutas.MapGet("/api/vencimientos", ConsultarVencimientosAsync)
            .RequireAuthorization(politica);
    }

    /// <summary>
    /// Una lista vacía significa que no hay vencimientos pendientes, y la pantalla lo dice
    /// explícitamente en vez de mostrar una tabla vacía (US5 esc. 4).
    /// </summary>
    private static async Task<IResult> ConsultarVencimientosAsync(
        ConsultarVencimientos consultar,
        CancellationToken cancelacion) =>
        Results.Ok(await consultar.EjecutarAsync(cancelacion));

    private static async Task<IResult> CargarAsync(
        int choferId,
        HttpRequest peticion,
        CargarDocumento cargar,
        CancellationToken cancelacion)
    {
        if (!peticion.HasFormContentType)
        {
            return Results.BadRequest(new ErrorResponse(
                CodigosErrorChoferes.DatosInvalidos,
                MensajesChoferes.DatosInvalidos));
        }

        var formulario = await peticion.ReadFormAsync(cancelacion);
        var resultado = await cargar.EjecutarAsync(
            choferId,
            LeerDocumento(formulario),
            LeerArchivo(formulario),
            cancelacion);

        if (resultado.Exitoso)
        {
            return Results.Created(
                $"/api/documentacion/{resultado.Documento!.Id}",
                resultado.Documento);
        }

        return TraducirFallo(resultado);
    }

    private static async Task<IResult> CorregirAsync(
        int id,
        HttpRequest peticion,
        CorregirDocumento corregir,
        CancellationToken cancelacion)
    {
        if (!peticion.HasFormContentType)
        {
            return Results.BadRequest(new ErrorResponse(
                CodigosErrorChoferes.DatosInvalidos,
                MensajesChoferes.DatosInvalidos));
        }

        var formulario = await peticion.ReadFormAsync(cancelacion);
        var resultado = await corregir.EjecutarAsync(
            id,
            LeerDocumento(formulario),
            LeerArchivo(formulario),
            cancelacion);

        return resultado.Exitoso
            ? Results.Ok(resultado.Documento)
            : TraducirFallo(resultado);
    }

    private static async Task<IResult> EliminarAsync(
        int id,
        EliminarDocumento eliminar,
        CancellationToken cancelacion)
    {
        var resultado = await eliminar.EjecutarAsync(id, cancelacion);

        return resultado.Exitoso ? Results.NoContent() : TraducirFallo(resultado);
    }

    private static async Task<IResult> DescargarAsync(
        int id,
        DescargarArchivoDocumento descargar,
        CancellationToken cancelacion)
    {
        var archivo = await descargar.EjecutarAsync(id, cancelacion);

        if (archivo is null)
        {
            return NoEncontrado();
        }

        return Results.File(archivo.Contenido, archivo.TipoContenido, archivo.Nombre);
    }

    private static DocumentoRequest LeerDocumento(IFormCollection formulario) => new(
        int.TryParse(formulario["documentacionTipoId"], out var tipoId) ? tipoId : null,
        formulario["numero"],
        formulario["fechaEmision"],
        formulario["fechaVencimiento"]);

    /// <summary>
    /// El adjunto, o <c>null</c> si no vino ninguno. Un documento sin escaneo es válido: queda como
    /// documentación sin respaldo, que el sistema distingue de una respaldada (FR-015).
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

    private static IResult NoEncontrado() => Results.NotFound(new ErrorResponse(
        CodigosErrorChoferes.NoEncontrado,
        MensajesChoferes.NoEncontrado));

    private static IResult TraducirFallo(ResultadoDocumento resultado) => resultado.Error switch
    {
        ErrorDocumento.NoEncontrado or ErrorDocumento.ChoferNoEncontrado => NoEncontrado(),

        ErrorDocumento.TipoInexistente => Results.BadRequest(new ErrorResponse(
            CodigosErrorChoferes.TipoInexistente,
            MensajesChoferes.TipoInexistente,
            resultado.Campo)),

        ErrorDocumento.VencimientoAnteriorAEmision => Results.BadRequest(new ErrorResponse(
            CodigosErrorChoferes.VencimientoAnteriorAEmision,
            MensajesChoferes.VencimientoAnteriorAEmision,
            resultado.Campo)),

        ErrorDocumento.ArchivoNoAdmitido => Results.BadRequest(new ErrorResponse(
            CodigosErrorChoferes.ArchivoNoAdmitido,
            MensajesChoferes.ArchivoNoAdmitido,
            resultado.Campo)),

        // 500 y no 400: el archivo era válido y el problema fue del sistema, no de lo que cargaron.
        // El documento no se creó ni se modificó, y la pantalla conserva lo tipeado (FR-015e).
        ErrorDocumento.ArchivoNoGuardado => Results.Json(
            new ErrorResponse(
                CodigosErrorChoferes.ArchivoNoGuardado,
                MensajesChoferes.ArchivoNoGuardado,
                resultado.Campo),
            statusCode: StatusCodes.Status500InternalServerError),

        _ => Results.BadRequest(new ErrorResponse(
            CodigosErrorChoferes.DatosInvalidos,
            MensajesChoferes.DatosInvalidos,
            resultado.Campo)),
    };
}
