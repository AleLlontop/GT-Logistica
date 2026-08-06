using GT.Api.Autorizacion;
using GT.Application.Autenticacion;
using GT.Application.Choferes;
using GT.Domain.Choferes;
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

        grupo.MapGet("/", ListarAsync);
        grupo.MapGet("/{id:int}", ObtenerAsync);
        grupo.MapPost("/", CrearAsync);
        grupo.MapPut("/{id:int}", ModificarAsync);
        grupo.MapDelete("/{id:int}", DarDeBajaAsync);
        grupo.MapPost("/{id:int}/reactivacion", ReactivarAsync);
    }

    /// <param name="estado">
    /// Omitido significa <b>sólo activos</b> (FR-022). Los inactivos se piden explícitamente; no hay
    /// forma de traer los dos a la vez, y es deliberado.
    /// </param>
    private static async Task<IResult> ListarAsync(
        string? apellido,
        string? dni,
        int? transportistaId,
        string? estado,
        string? estadoDocumentacion,
        int? pagina,
        ConsultarChoferes consultar,
        CancellationToken cancelacion)
    {
        var filtros = new FiltrosDeChoferes(
            apellido,
            dni,
            transportistaId,
            SoloActivos: estado switch
            {
                "activo" => true,
                "inactivo" => false,
                _ => null,
            },
            EstadoDocumentacion: LeerEstadoDocumentacion(estadoDocumentacion),
            Pagina: pagina ?? 1);

        return Results.Ok(await consultar.EjecutarAsync(filtros, cancelacion));
    }

    private static async Task<IResult> ObtenerAsync(
        int id,
        ConsultarFichaChofer consultar,
        CancellationToken cancelacion)
    {
        var chofer = await consultar.EjecutarAsync(id, cancelacion);

        return chofer is not null ? Results.Ok(chofer) : NoEncontrado();
    }

    /// <summary>
    /// El contrato usa camelCase (<c>enRegla</c>, <c>proximaAvencer</c>), que no es la grafía del
    /// enum. Un valor desconocido se ignora en vez de romper: filtrar de más no es un error.
    /// </summary>
    private static EstadoDocumentacionChofer? LeerEstadoDocumentacion(string? valor) => valor switch
    {
        "enRegla" => EstadoDocumentacionChofer.EnRegla,
        "proximaAvencer" => EstadoDocumentacionChofer.ProximaAvencer,
        "vencida" => EstadoDocumentacionChofer.Vencida,
        "sinDocumentacion" => EstadoDocumentacionChofer.SinDocumentacion,
        _ => null,
    };

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

    private static async Task<IResult> ModificarAsync(
        int id,
        ChoferRequest peticion,
        ModificarChofer modificar,
        CancellationToken cancelacion)
    {
        var resultado = await modificar.EjecutarAsync(id, peticion, cancelacion);

        if (resultado.Exitoso)
        {
            return Results.Ok(resultado.Chofer);
        }

        return resultado.Error is ErrorChofer.NoEncontrado
            ? NoEncontrado()
            : Results.BadRequest(TraducirError(resultado));
    }

    private static async Task<IResult> DarDeBajaAsync(
        int id,
        DarDeBajaChofer darDeBaja,
        CancellationToken cancelacion)
    {
        var resultado = await darDeBaja.EjecutarAsync(id, cancelacion);

        return resultado.Exitoso ? Results.NoContent() : NoEncontrado();
    }

    private static async Task<IResult> ReactivarAsync(
        int id,
        ReactivarChofer reactivar,
        CancellationToken cancelacion)
    {
        var resultado = await reactivar.EjecutarAsync(id, cancelacion);

        if (resultado.Exitoso)
        {
            return Results.NoContent();
        }

        return resultado.Error is ErrorChofer.NoEncontrado
            ? NoEncontrado()
            : Results.BadRequest(TraducirError(resultado));
    }

    private static IResult NoEncontrado() => Results.NotFound(new ErrorResponse(
        CodigosErrorChoferes.NoEncontrado,
        MensajesChoferes.NoEncontrado));

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
