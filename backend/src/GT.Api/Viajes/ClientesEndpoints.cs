using GT.Api.Autorizacion;
using GT.Application.Autenticacion;
using GT.Application.Viajes;
using GT.Application.Viajes.Clientes;
using GT.Domain.Usuarios;

namespace GT.Api.Viajes;

/// <summary>
/// Padrón de clientes (FR-001 a FR-009).
///
/// <b>Los dos permisos se reparten por operación, no por grupo</b> (FR-053): los <c>GET</c> exigen
/// <c>viajes.consultar</c> —Gerencia y Administración de la empresa consultan el padrón sin poder
/// tocarlo— y las escrituras exigen <c>viajes.gestionar</c>. La restricción no vive sólo en la
/// pantalla: quien invoque la acción a mano recibe <c>403</c> (FR-052, SC-012).
/// </summary>
public static class ClientesEndpoints
{
    public static void MapearClientes(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/clientes");

        var consultar = PoliticasAutorizacion.Para(CodigosPermiso.ViajesConsultar);
        var gestionar = PoliticasAutorizacion.Para(CodigosPermiso.ViajesGestionar);

        // Todas las rutas con identificador llevan `{id:int}`. Este grupo no tiene rutas literales
        // que puedan competir, pero la restricción va igual: es la convención del módulo y evita que
        // una ruta literal futura quede inalcanzable sin que nadie lo note (tasks §trampa 1).
        grupo.MapGet("/", ListarAsync).RequireAuthorization(consultar);
        grupo.MapGet("/{id:int}", ObtenerAsync).RequireAuthorization(consultar);
        grupo.MapPost("/", CrearAsync).RequireAuthorization(gestionar);
        grupo.MapPut("/{id:int}", ModificarAsync).RequireAuthorization(gestionar);
        grupo.MapDelete("/{id:int}", DarDeBajaAsync).RequireAuthorization(gestionar);
        grupo.MapPost("/{id:int}/alta", DarDeAltaAsync).RequireAuthorization(gestionar);
    }

    /// <param name="soloActivos">
    /// Anulable con <c>?? false</c>: como <c>bool</c> a secas, pedir el listado sin el parámetro
    /// falla al enlazar en vez de tomar el valor por defecto (convención [003]).
    /// </param>
    private static async Task<IResult> ListarAsync(
        bool? soloActivos,
        string? busqueda,
        int? pagina,
        ConsultarClientes consultar,
        CancellationToken cancelacion)
    {
        var filtros = new FiltrosDeClientes(soloActivos ?? false, busqueda, pagina ?? 1);

        return Results.Ok(await consultar.EjecutarAsync(filtros, cancelacion));
    }

    private static async Task<IResult> ObtenerAsync(
        int id,
        IRepositorioClientes clientes,
        CancellationToken cancelacion)
    {
        var cliente = await clientes.ObtenerPorIdAsync(id, cancelacion);

        return cliente is not null ? Results.Ok(ClienteDto.Desde(cliente)) : NoEncontrado();
    }

    private static async Task<IResult> CrearAsync(
        ClienteRequest peticion,
        CrearCliente crear,
        CancellationToken cancelacion)
    {
        var resultado = await crear.EjecutarAsync(peticion, cancelacion);

        return resultado.Exitoso
            ? Results.Created($"/api/clientes/{resultado.Cliente!.Id}", resultado.Cliente)
            : TraducirFallo(resultado);
    }

    private static async Task<IResult> ModificarAsync(
        int id,
        ClienteRequest peticion,
        ModificarCliente modificar,
        CancellationToken cancelacion)
    {
        var resultado = await modificar.EjecutarAsync(id, peticion, cancelacion);

        return resultado.Exitoso ? Results.Ok(resultado.Cliente) : TraducirFallo(resultado);
    }

    /// <summary>
    /// Baja lógica. La confirmación previa la pide la pantalla, no el endpoint: la baja se deshace
    /// con el alta (FR-005).
    /// </summary>
    private static async Task<IResult> DarDeBajaAsync(
        int id,
        DarDeBajaCliente darDeBaja,
        CancellationToken cancelacion)
    {
        var resultado = await darDeBaja.EjecutarAsync(id, cancelacion);

        return resultado.Exitoso ? Results.NoContent() : TraducirFallo(resultado);
    }

    /// <summary>Idempotente y sin confirmación aparte (FR-007).</summary>
    private static async Task<IResult> DarDeAltaAsync(
        int id,
        DarDeAltaCliente darDeAlta,
        CancellationToken cancelacion)
    {
        var resultado = await darDeAlta.EjecutarAsync(id, cancelacion);

        return resultado.Exitoso ? Results.NoContent() : TraducirFallo(resultado);
    }

    private static IResult NoEncontrado() => Results.NotFound(
        new ErrorResponse(CodigosErrorViajes.NoEncontrado, MensajesViajes.NoEncontrado));

    private static IResult TraducirFallo(ResultadoCliente resultado) => resultado.Error switch
    {
        ErrorCliente.NoEncontrado => NoEncontrado(),

        ErrorCliente.CuitInvalido => Error(
            CodigosErrorViajes.CuitInvalido, MensajesViajes.CuitInvalido, resultado.Campo),

        ErrorCliente.CuitDuplicado => Error(
            CodigosErrorViajes.CuitDuplicado, MensajesViajes.CuitDuplicado, resultado.Campo),

        ErrorCliente.CuitDeClienteDadoDeBaja => Error(
            CodigosErrorViajes.CuitDeClienteDadoDeBaja,
            MensajesViajes.CuitDeClienteDadoDeBaja,
            resultado.Campo),

        ErrorCliente.EmailInvalido => Error(
            CodigosErrorViajes.EmailInvalido, MensajesViajes.EmailInvalido, resultado.Campo),

        // La cantidad va en el cuerpo además de en el mensaje: saber que hay dependientes sin saber
        // cuántos no ayuda a resolverlo (FR-006, SC-009, precedente [004]).
        ErrorCliente.ConViajes => Results.BadRequest(new ErrorConDependencias(
            CodigosErrorViajes.ClienteConViajes,
            MensajesViajes.ClienteConViajes(resultado.CantidadViajes ?? 0))
        {
            CantidadViajes = resultado.CantidadViajes,
        }),

        // Cualquier error sin código propio se comunica como datos inválidos, con el campo marcado.
        // Nunca se cae: una respuesta de error no puede convertirse en un 500.
        _ => Error(CodigosErrorViajes.DatosInvalidos, MensajesViajes.DatosInvalidos, resultado.Campo),
    };

    private static IResult Error(string codigo, string mensaje, string? campo) =>
        Results.BadRequest(new ErrorResponse(codigo, mensaje, campo));
}
