using GT.Api.Autorizacion;
using GT.Application.Autenticacion;
using GT.Application.Choferes;
using GT.Domain.Usuarios;

namespace GT.Api.Choferes;

/// <summary>
/// Registro de choferes (FR-005 a FR-011).
///
/// Todo el grupo exige el permiso <c>choferes.gestionar</c>, que otorgan los roles Tráfico y
/// Administrador del sistema (FR-027).
///
/// Por ahora, sólo el alta: el listado y la ficha llegan con la User Story 4, y la modificación, la
/// baja y la reactivación con la User Story 7.
/// </summary>
public static class ChoferesEndpoints
{
    public static void MapearChoferes(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/choferes")
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.ChoferesGestionar));

        grupo.MapPost("/", CrearAsync);
    }

    private static async Task<IResult> CrearAsync(
        ChoferRequest peticion,
        CrearChofer crear,
        CancellationToken cancelacion)
    {
        var resultado = await crear.EjecutarAsync(peticion, cancelacion);

        return resultado.Exitoso
            ? Results.Created($"/api/choferes/{resultado.Chofer!.Id}", resultado.Chofer)
            : Results.BadRequest(TraducirError(resultado));
    }

    private static ErrorResponse TraducirError(ResultadoChofer resultado) => resultado.Error switch
    {
        ErrorChofer.DniDuplicado => new ErrorResponse(
            CodigosErrorChoferes.DniDuplicado,
            MensajesChoferes.DniDuplicado,
            resultado.Campo),

        ErrorChofer.CuilDuplicado => new ErrorResponse(
            CodigosErrorChoferes.CuilDuplicado,
            MensajesChoferes.CuilDuplicado,
            resultado.Campo),

        ErrorChofer.TransportistaInexistente => new ErrorResponse(
            CodigosErrorChoferes.TransportistaInexistente,
            MensajesChoferes.TransportistaInexistente,
            resultado.Campo),

        ErrorChofer.MenorDeEdad => new ErrorResponse(
            CodigosErrorChoferes.MenorDeEdad,
            MensajesChoferes.MenorDeEdad,
            resultado.Campo),

        ErrorChofer.NoEncontrado => new ErrorResponse(
            CodigosErrorChoferes.NoEncontrado,
            MensajesChoferes.NoEncontrado,
            resultado.Campo),

        // Cualquier error que no tenga un código propio se comunica como datos inválidos, con el
        // campo marcado. Nunca se cae: una respuesta de error no puede convertirse en un 500.
        _ => new ErrorResponse(
            CodigosErrorChoferes.DatosInvalidos,
            MensajesChoferes.DatosInvalidos,
            resultado.Campo),
    };
}
