using GT.Api.Autorizacion;
using GT.Application.Autenticacion;
using GT.Application.Usuarios;
using GT.Application.Usuarios.Personas;
using GT.Domain.Usuarios;

namespace GT.Api.Usuarios.Personas;

/// <summary>
/// Padrón de personas (choferes y empleados).
///
/// Todo el grupo exige el permiso <c>usuarios.gestionar</c>: el padrón es parte de este módulo y
/// comparte su restricción de acceso, sin un permiso propio (FR-007, research §7).
///
/// Por ahora sólo lectura: el alta, la edición y la baja llegan con la User Story 6. La lectura va
/// primero porque el selector de persona del formulario de alta la necesita para poder mostrar su
/// estado vacío en vez de un error.
/// </summary>
public static class PersonasEndpoints
{
    public static void MapearPersonas(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/personas")
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.UsuariosGestionar));

        grupo.MapGet("/", ListarAsync);
        grupo.MapGet("/{id:int}", ObtenerAsync);
        grupo.MapPost("/", CrearAsync);
        grupo.MapPut("/{id:int}", ModificarAsync);
        grupo.MapDelete("/{id:int}", DarDeBajaAsync);
    }

    private static async Task<IResult> CrearAsync(
        PersonaRequest peticion,
        CrearPersona crear,
        CancellationToken cancelacion)
    {
        var resultado = await crear.EjecutarAsync(peticion, cancelacion);

        return resultado.Exitoso
            ? Results.Created($"/api/personas/{resultado.Persona!.Id}", resultado.Persona)
            : Results.BadRequest(TraducirError(resultado));
    }

    private static async Task<IResult> ModificarAsync(
        int id,
        PersonaRequest peticion,
        ModificarPersona modificar,
        CancellationToken cancelacion)
    {
        var resultado = await modificar.EjecutarAsync(id, peticion, cancelacion);

        if (resultado.Exitoso)
        {
            return Results.Ok(resultado.Persona);
        }

        return resultado.Error is ErrorPersona.NoEncontrada
            ? NoEncontrada()
            : Results.BadRequest(TraducirError(resultado));
    }

    /// <summary>
    /// Baja lógica (FR-022). Se rechaza si la persona está vinculada a un usuario, cualquiera sea el
    /// estado de esa cuenta (FR-028).
    /// </summary>
    private static async Task<IResult> DarDeBajaAsync(
        int id,
        DarDeBajaPersona darDeBaja,
        CancellationToken cancelacion)
    {
        var resultado = await darDeBaja.EjecutarAsync(id, cancelacion);

        if (resultado.Exitoso)
        {
            return Results.NoContent();
        }

        return resultado.Error is ErrorPersona.NoEncontrada
            ? NoEncontrada()
            : Results.BadRequest(TraducirError(resultado));
    }

    private static IResult NoEncontrada() => Results.NotFound(new ErrorResponse(
        CodigosErrorUsuarios.NoEncontrado,
        MensajesUsuarios.NoEncontrado));

    private static ErrorResponse TraducirError(ResultadoPersona resultado) => resultado.Error switch
    {
        ErrorPersona.DniDuplicado => new ErrorResponse(
            CodigosErrorUsuarios.DniDuplicado,
            MensajesUsuarios.DniDuplicado,
            resultado.Campo),

        ErrorPersona.Vinculada => new ErrorResponse(
            CodigosErrorUsuarios.PersonaVinculada,
            MensajesUsuarios.PersonaVinculada(resultado.UsernameQueLaTiene ?? "otro"),
            resultado.Campo),

        ErrorPersona.EsChofer => new ErrorResponse(
            CodigosErrorUsuarios.PersonaEsChofer,
            MensajesUsuarios.PersonaEsChofer,
            resultado.Campo),

        _ => new ErrorResponse(
            CodigosErrorUsuarios.DatosInvalidos,
            MensajesUsuarios.DatosInvalidos,
            resultado.Campo),
    };

    /// <summary>
    /// Lista el padrón. Una lista vacía es una respuesta legítima, no un error: es el estado de toda
    /// instalación nueva (FR-024), y la pantalla lo informa con un mensaje explícito (FR-025).
    /// </summary>
    private static async Task<IResult> ListarAsync(
        ConsultarPersonas consultar,
        string? texto,
        bool? soloActivas,
        CancellationToken cancelacion)
    {
        var personas = await consultar.EjecutarAsync(texto, soloActivas ?? false, cancelacion);

        return Results.Ok(personas);
    }

    private static async Task<IResult> ObtenerAsync(
        int id,
        ConsultarPersonas consultar,
        CancellationToken cancelacion)
    {
        var persona = await consultar.ObtenerAsync(id, cancelacion);

        return persona is null
            ? Results.NotFound(new ErrorResponse(
                CodigosErrorUsuarios.NoEncontrado,
                MensajesUsuarios.NoEncontrado))
            : Results.Ok(persona);
    }
}
