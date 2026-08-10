using GT.Api.Autorizacion;
using GT.Application.Autenticacion;
using GT.Application.Choferes;
using GT.Application.Choferes.Documentacion;
using GT.Application.Flota;
using GT.Domain.Usuarios;

namespace GT.Api.Choferes;

/// <summary>
/// Catálogo de tipos de documentación (FR-013, FR-014).
///
/// Arranca vacío y no se precarga por migración: el primer tipo lo carga quien opera, desde la
/// pantalla. Sin al menos uno no se puede registrar ningún documento.
///
/// <b>Sirve a dos módulos desde el Módulo 4</b> y por eso cada tipo declara su ámbito: el ABM sigue
/// viviendo acá, bajo <c>choferes.gestionar</c>, y no se duplica en flota (Módulo 4, FR-017,
/// research §3).
/// </summary>
public static class TiposDocumentacionEndpoints
{
    public static void MapearTiposDocumentacion(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/tipos-documentacion")
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.ChoferesGestionar));

        grupo.MapGet("/", ListarAsync);
        grupo.MapPost("/", CrearAsync);
        grupo.MapPut("/{id:int}", ModificarAsync);
        grupo.MapDelete("/{id:int}", DarDeBajaAsync);
    }

    /// <param name="soloActivos">
    /// Anulable a propósito: pedir el catálogo sin el parámetro tiene que tomar el valor por defecto
    /// del contrato en vez de fallar al enlazar.
    /// </param>
    /// <param name="ambito">
    /// Filtra por ámbito (Módulo 4, FR-017a). Omitido devuelve los dos, que es lo que muestra la
    /// pantalla de mantenimiento. Un valor desconocido se ignora en vez de romper: filtrar de más no
    /// es un error.
    /// </param>
    private static async Task<IResult> ListarAsync(
        bool? soloActivos,
        string? ambito,
        GestionTiposDocumentacion gestion,
        CancellationToken cancelacion) =>
        Results.Ok(await gestion.ConsultarAsync(
            soloActivos ?? false,
            ValidadorTipoDocumentacion.LeerAmbito(ambito),
            cancelacion));

    private static async Task<IResult> CrearAsync(
        TipoDocumentacionRequest peticion,
        GestionTiposDocumentacion gestion,
        CancellationToken cancelacion)
    {
        var resultado = await gestion.CrearAsync(peticion, cancelacion);

        return resultado.Exitoso
            ? Results.Created($"/api/tipos-documentacion/{resultado.Tipo!.Id}", resultado.Tipo)
            : Results.BadRequest(TraducirError(resultado));
    }

    private static async Task<IResult> ModificarAsync(
        int id,
        TipoDocumentacionRequest peticion,
        GestionTiposDocumentacion gestion,
        CancellationToken cancelacion)
    {
        var resultado = await gestion.ModificarAsync(id, peticion, cancelacion);

        if (resultado.Exitoso)
        {
            return Results.Ok(resultado.Tipo);
        }

        return resultado.Error switch
        {
            ErrorTipoDocumentacion.NoEncontrado => NoEncontrado(),

            // 409 y no 400: los datos que mandaron son válidos; lo que impide el cambio es el estado
            // del tipo, y el cuerpo dice cuántos documentos son (Módulo 4, FR-017d).
            ErrorTipoDocumentacion.AmbitoNoModificable => Results.Json(
                new ErrorConDependencias(
                    CodigosErrorChoferes.AmbitoNoModificable,
                    MensajesChoferes.AmbitoNoModificable(resultado.CantidadDocumentos ?? 0),
                    resultado.Campo)
                {
                    CantidadDocumentos = resultado.CantidadDocumentos,
                },
                statusCode: StatusCodes.Status409Conflict),

            _ => Results.BadRequest(TraducirError(resultado)),
        };
    }

    private static async Task<IResult> DarDeBajaAsync(
        int id,
        GestionTiposDocumentacion gestion,
        CancellationToken cancelacion)
    {
        var resultado = await gestion.DarDeBajaAsync(id, cancelacion);

        if (resultado.Exitoso)
        {
            return Results.NoContent();
        }

        return resultado.Error is ErrorTipoDocumentacion.NoEncontrado
            ? NoEncontrado()
            : Results.BadRequest(TraducirError(resultado));
    }

    private static IResult NoEncontrado() => Results.NotFound(new ErrorResponse(
        CodigosErrorChoferes.NoEncontrado,
        MensajesChoferes.NoEncontrado));

    private static ErrorResponse TraducirError(ResultadoTipoDocumentacion resultado) => resultado.Error switch
    {
        ErrorTipoDocumentacion.NombreDuplicado => new ErrorResponse(
            CodigosErrorChoferes.TipoDuplicado,
            MensajesChoferes.TipoDuplicado,
            resultado.Campo),

        ErrorTipoDocumentacion.ConDocumentos => new ErrorResponse(
            CodigosErrorChoferes.TipoConDocumentos,
            MensajesChoferes.TipoConDocumentos(resultado.CantidadDocumentos ?? 0),
            resultado.Campo),

        ErrorTipoDocumentacion.NoEncontrado => new ErrorResponse(
            CodigosErrorChoferes.NoEncontrado,
            MensajesChoferes.NoEncontrado,
            resultado.Campo),

        _ => new ErrorResponse(
            CodigosErrorChoferes.DatosInvalidos,
            MensajesChoferes.DatosInvalidos,
            resultado.Campo),
    };
}
