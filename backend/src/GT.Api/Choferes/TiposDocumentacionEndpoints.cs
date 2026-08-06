using GT.Api.Autorizacion;
using GT.Application.Autenticacion;
using GT.Application.Choferes;
using GT.Application.Choferes.Documentacion;
using GT.Domain.Usuarios;

namespace GT.Api.Choferes;

/// <summary>
/// Catálogo de tipos de documentación (FR-013, FR-014).
///
/// Arranca vacío y no se precarga por migración: el primer tipo lo carga quien opera, desde la
/// pantalla. Sin al menos uno no se puede registrar ningún documento.
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
    private static async Task<IResult> ListarAsync(
        bool? soloActivos,
        GestionTiposDocumentacion gestion,
        CancellationToken cancelacion) =>
        Results.Ok(await gestion.ConsultarAsync(soloActivos ?? false, cancelacion));

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

        return resultado.Error is ErrorTipoDocumentacion.NoEncontrado
            ? NoEncontrado()
            : Results.BadRequest(TraducirError(resultado));
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
