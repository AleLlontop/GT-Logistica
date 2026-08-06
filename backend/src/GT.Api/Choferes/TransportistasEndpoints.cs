using GT.Api.Autorizacion;
using GT.Application.Autenticacion;
using GT.Application.Choferes;
using GT.Application.Choferes.Transportistas;
using GT.Domain.Usuarios;

namespace GT.Api.Choferes;

/// <summary>
/// Padrón de transportistas (FR-002, FR-003).
///
/// Todo el grupo exige el permiso <c>choferes.gestionar</c>, que otorgan los roles Tráfico y
/// Administrador del sistema (FR-027).
///
/// Por ahora, alta y consulta: la modificación y la baja llegan con la User Story 7.
/// </summary>
public static class TransportistasEndpoints
{
    public static void MapearTransportistas(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/transportistas")
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.ChoferesGestionar));

        grupo.MapGet("/", ListarAsync);
        grupo.MapGet("/{id:int}", ObtenerAsync);
        grupo.MapPost("/", CrearAsync);
        grupo.MapPut("/{id:int}", ModificarAsync);
        grupo.MapDelete("/{id:int}", DarDeBajaAsync);
    }

    /// <param name="soloActivos">
    /// Anulable a propósito: si se declarara como <c>bool</c>, pedir el listado sin el parámetro
    /// —que es lo que hace la pantalla al entrar— fallaría al enlazar en vez de tomar el valor por
    /// defecto que fija el contrato. Es la misma forma que usa el padrón de personas del Módulo 2.
    /// </param>
    private static async Task<IResult> ListarAsync(
        string? texto,
        bool? soloActivos,
        ConsultarTransportistas consultar,
        CancellationToken cancelacion)
    {
        // Una lista vacía es una respuesta legítima: es el estado de toda instalación nueva, y la
        // pantalla lo informa con un mensaje explícito (FR-023).
        return Results.Ok(await consultar.EjecutarAsync(texto, soloActivos ?? false, cancelacion));
    }

    private static async Task<IResult> ObtenerAsync(
        int id,
        ConsultarTransportistaPorId consultar,
        CancellationToken cancelacion)
    {
        var resultado = await consultar.EjecutarAsync(id, cancelacion);

        return resultado.Exitoso
            ? Results.Ok(resultado.Transportista)
            : NoEncontrado();
    }

    private static async Task<IResult> CrearAsync(
        TransportistaRequest peticion,
        CrearTransportista crear,
        CancellationToken cancelacion)
    {
        var resultado = await crear.EjecutarAsync(peticion, cancelacion);

        return resultado.Exitoso
            ? Results.Created($"/api/transportistas/{resultado.Transportista!.Id}", resultado.Transportista)
            : Results.BadRequest(TraducirError(resultado));
    }

    private static async Task<IResult> ModificarAsync(
        int id,
        TransportistaRequest peticion,
        ModificarTransportista modificar,
        CancellationToken cancelacion)
    {
        var resultado = await modificar.EjecutarAsync(id, peticion, cancelacion);

        if (resultado.Exitoso)
        {
            return Results.Ok(resultado.Transportista);
        }

        return resultado.Error is ErrorTransportista.NoEncontrado
            ? NoEncontrado()
            : Results.BadRequest(TraducirError(resultado));
    }

    private static async Task<IResult> DarDeBajaAsync(
        int id,
        DarDeBajaTransportista darDeBaja,
        CancellationToken cancelacion)
    {
        var resultado = await darDeBaja.EjecutarAsync(id, cancelacion);

        if (resultado.Exitoso)
        {
            return Results.NoContent();
        }

        return resultado.Error is ErrorTransportista.NoEncontrado
            ? NoEncontrado()
            : Results.BadRequest(TraducirError(resultado));
    }

    private static IResult NoEncontrado() => Results.NotFound(new ErrorResponse(
        CodigosErrorChoferes.NoEncontrado,
        MensajesChoferes.NoEncontrado));

    private static ErrorResponse TraducirError(ResultadoTransportista resultado) => resultado.Error switch
    {
        ErrorTransportista.CuitDuplicado => new ErrorResponse(
            CodigosErrorChoferes.CuitDuplicado,
            MensajesChoferes.CuitDuplicado,
            resultado.Campo),

        ErrorTransportista.ConChoferes => new ErrorResponse(
            CodigosErrorChoferes.TransportistaConChoferes,
            MensajesChoferes.TransportistaConChoferes(resultado.CantidadChoferes ?? 0),
            resultado.Campo),

        ErrorTransportista.NoEncontrado => new ErrorResponse(
            CodigosErrorChoferes.NoEncontrado,
            MensajesChoferes.NoEncontrado,
            resultado.Campo),

        _ => new ErrorResponse(
            CodigosErrorChoferes.DatosInvalidos,
            MensajesChoferes.DatosInvalidos,
            resultado.Campo),
    };
}
