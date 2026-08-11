using GT.Api.Autorizacion;
using GT.Application.Viajes;
using GT.Domain.Usuarios;

namespace GT.Api.Viajes;

/// <summary>
/// Asignación de chofer y vehículo (FR-019 a FR-030).
///
/// <b>Recurso propio y no un campo del <c>PUT</c></b>: es la única operación del módulo que devuelve
/// bloqueos y advertencias por documentación, y sacarla del guardado de datos deja las dos respuestas
/// limpias (FR-019a, research §4).
///
/// <b>La ruta literal <c>asignables</c> convive con <c>/api/viajes/{id:int}</c></b>, que está
/// declarada en <see cref="ViajesEndpoints"/> con la restricción de tipo. Sin ella las dos serían
/// ambiguas y esta quedaría inalcanzable, porque el enrutador trataría <c>asignables</c> como un
/// identificador (tasks §trampa 1).
///
/// Las dos operaciones exigen <c>viajes.gestionar</c>: la lista de asignables sólo le sirve a quien
/// puede asignar.
/// </summary>
public static class AsignacionEndpoints
{
    public static void MapearAsignacion(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/viajes")
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.ViajesGestionar));

        grupo.MapGet("/asignables", ListarAsignablesAsync);
        grupo.MapPost("/{id:int}/asignacion", AsignarAsync);
    }

    private static async Task<IResult> ListarAsignablesAsync(
        ConsultarAsignables consultar,
        CancellationToken cancelacion) =>
        Results.Ok(await consultar.EjecutarAsync(cancelacion));

    /// <summary>
    /// Devuelve el sobre <c>{ viaje, advertencias }</c>: es una de las tres operaciones que pueden
    /// advertir sin frenar el guardado (FR-015a).
    ///
    /// El bloqueo por documentación sale como <c>409</c> y no como <c>400</c>: el problema no está en
    /// lo que se tipeó —el chofer elegido existe y está activo— sino en el estado de algo que cambió
    /// afuera de este módulo (research §5).
    /// </summary>
    private static async Task<IResult> AsignarAsync(
        int id,
        AsignacionRequest peticion,
        AsignarChoferYVehiculo asignar,
        CancellationToken cancelacion)
    {
        var resultado = await asignar.EjecutarAsync(id, peticion, cancelacion);

        return resultado.Exitoso
            ? Results.Ok(resultado.Sobre())
            : RespuestasDeViaje.TraducirFallo(resultado);
    }
}
