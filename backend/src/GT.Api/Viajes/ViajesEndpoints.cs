using System.Security.Claims;
using GT.Api.Autenticacion;
using GT.Api.Autorizacion;
using GT.Application.Viajes;
using GT.Domain.Usuarios;

namespace GT.Api.Viajes;

/// <summary>
/// Listado, ficha, alta y edición de viajes (FR-010 a FR-018, FR-040 a FR-045).
///
/// <b>Las dos rutas con identificador llevan <c>{id:int}</c>, y no es decorativo</b>: en este mismo
/// prefijo conviven <c>/api/viajes/asignables</c> y <c>/api/viajes/totales</c>. Sin la restricción,
/// <c>/api/viajes/{id}</c> las capturaría a las tres y las dos literales quedarían inalcanzables
/// —el enrutador las trataría como identificadores y fallaría al convertirlas— (tasks §trampa 1).
///
/// Los <c>GET</c> exigen <c>viajes.consultar</c> y las escrituras <c>viajes.gestionar</c> (FR-053).
/// </summary>
public static class ViajesEndpoints
{
    public static void MapearViajes(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/viajes");

        var consultar = PoliticasAutorizacion.Para(CodigosPermiso.ViajesConsultar);
        var gestionar = PoliticasAutorizacion.Para(CodigosPermiso.ViajesGestionar);

        grupo.MapGet("/", ListarAsync).RequireAuthorization(consultar);
        grupo.MapGet("/{id:int}", ObtenerAsync).RequireAuthorization(consultar);
        grupo.MapPost("/", CrearAsync).RequireAuthorization(gestionar);
        grupo.MapPut("/{id:int}", ModificarAsync).RequireAuthorization(gestionar);
    }

    /// <param name="estado">
    /// Omitirlo <b>no</b> es lo mismo que pedir los cuatro: sin el parámetro no se devuelven los
    /// anulados (FR-044). Un valor desconocido se ignora en vez de romper: filtrar de más no es un
    /// error (convención [003]).
    /// </param>
    /// <param name="pagina">
    /// Anulable a propósito, igual que los booleanos de query del proyecto: pedir el listado sin el
    /// parámetro tiene que tomar el valor por defecto en vez de fallar al enlazar (convención [003]).
    /// </param>
    private static async Task<IResult> ListarAsync(
        int? clienteId,
        int? transportistaId,
        string? estado,
        DateOnly? desde,
        DateOnly? hasta,
        string? busqueda,
        int? pagina,
        ConsultarViajes consultar,
        CancellationToken cancelacion)
    {
        var filtros = new FiltrosDeViajes(
            clienteId,
            transportistaId,
            NombresDeEstadoViaje.Leer(estado),
            desde,
            hasta,
            busqueda,
            pagina ?? 1);

        return Results.Ok(await consultar.EjecutarAsync(filtros, cancelacion));
    }

    private static async Task<IResult> ObtenerAsync(
        int id,
        ConsultarFichaViaje consultar,
        CancellationToken cancelacion)
    {
        var viaje = await consultar.EjecutarAsync(id, cancelacion);

        return viaje is not null ? Results.Ok(viaje) : RespuestasDeViaje.NoEncontrado();
    }

    /// <summary>
    /// Devuelve el sobre <c>{ viaje, advertencias }</c>: es una de las tres operaciones que pueden
    /// advertir sin frenar el guardado (FR-015a).
    ///
    /// El usuario de la sesión se lee acá y se pasa <b>por parámetro</b> al caso de uso, en vez de
    /// introducir una abstracción de usuario actual que hoy tendría cuatro llamadores (research §7).
    /// </summary>
    private static async Task<IResult> CrearAsync(
        ViajeRequest peticion,
        ClaimsPrincipal principal,
        CrearViaje crear,
        CancellationToken cancelacion)
    {
        var usuarioId = ClaimsSesion.ObtenerIdUsuario(principal);

        if (usuarioId is null)
        {
            return Results.Unauthorized();
        }

        var resultado = await crear.EjecutarAsync(peticion, usuarioId.Value, cancelacion);

        return resultado.Exitoso
            ? Results.Created($"/api/viajes/{resultado.Viaje!.Id}", resultado.Sobre())
            : RespuestasDeViaje.TraducirFallo(resultado);
    }

    /// <summary>
    /// El cuerpo <b>no</b> lleva número, estado, chofer ni vehículo: no están en
    /// <see cref="ViajeRequest"/>, así que no hay nada que ignorar (FR-011, FR-019a, FR-034).
    /// </summary>
    private static async Task<IResult> ModificarAsync(
        int id,
        ViajeRequest peticion,
        ModificarViaje modificar,
        CancellationToken cancelacion)
    {
        var resultado = await modificar.EjecutarAsync(id, peticion, cancelacion);

        return resultado.Exitoso
            ? Results.Ok(resultado.Sobre())
            : RespuestasDeViaje.TraducirFallo(resultado);
    }
}
