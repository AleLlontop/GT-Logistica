using System.Security.Claims;
using GT.Api.Autenticacion;
using GT.Api.Autorizacion;
using GT.Application.Viajes;
using GT.Domain.Usuarios;

namespace GT.Api.Viajes;

/// <summary>
/// Las tres transiciones del ciclo de vida (FR-033, FR-034).
///
/// <b>Cada una es un recurso propio y nunca un campo del <c>PUT</c></b>: así corregir un destino no
/// puede avanzar ni anular un viaje en silencio. Es el precedente [004] —cambiar el estado de una
/// entidad es un recurso propio— aplicado a un ciclo de vida completo.
///
/// Las tres exigen <c>viajes.gestionar</c> y llevan <c>{id:int}</c> en la ruta (tasks §trampa 1).
///
/// <b>El usuario de la sesión se lee acá y viaja por parámetro</b> al caso de uso: no se introduce
/// una abstracción de usuario actual que hoy tendría cuatro llamadores (FR-034, FR-035, research §7).
/// </summary>
public static class CicloDeVidaEndpoints
{
    public static void MapearCicloDeVida(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas
            .MapGroup("/api/viajes")
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.ViajesGestionar));

        grupo.MapPost("/{id:int}/en-curso", PonerEnCursoAsync);
        grupo.MapPost("/{id:int}/rendicion", RendirAsync);
        grupo.MapPost("/{id:int}/anulacion", AnularAsync);
    }

    private static async Task<IResult> PonerEnCursoAsync(
        int id,
        ClaimsPrincipal principal,
        PonerViajeEnCurso ponerEnCurso,
        CancellationToken cancelacion)
    {
        if (ClaimsSesion.ObtenerIdUsuario(principal) is not { } usuarioId)
        {
            return Results.Unauthorized();
        }

        var resultado = await ponerEnCurso.EjecutarAsync(id, usuarioId, cancelacion);

        return resultado.Exitoso
            ? Results.Ok(resultado.Viaje)
            : RespuestasDeViaje.TraducirFallo(resultado);
    }

    /// <param name="peticion">
    /// Cuerpo <b>opcional</b>: <c>{ "confirmado": true }</c> sólo hace falta cuando el importe es
    /// cero, y es el segundo intento el que rinde. El primero responde <c>409</c> sin cambiar nada
    /// (FR-038, SC-007a).
    /// </param>
    private static async Task<IResult> RendirAsync(
        int id,
        RendicionRequest? peticion,
        ClaimsPrincipal principal,
        RendirViaje rendir,
        CancellationToken cancelacion)
    {
        if (ClaimsSesion.ObtenerIdUsuario(principal) is not { } usuarioId)
        {
            return Results.Unauthorized();
        }

        var resultado = await rendir.EjecutarAsync(id, peticion, usuarioId, cancelacion);

        return resultado.Exitoso
            ? Results.Ok(resultado.Viaje)
            : RespuestasDeViaje.TraducirFallo(resultado);
    }

    /// <param name="peticion">
    /// El motivo es <b>obligatorio</b> (FR-036). La confirmación explícita la pide la pantalla, que
    /// no habilita el botón sin motivo escrito; el endpoint verifica el motivo igual.
    /// </param>
    private static async Task<IResult> AnularAsync(
        int id,
        AnulacionRequest? peticion,
        ClaimsPrincipal principal,
        AnularViaje anular,
        CancellationToken cancelacion)
    {
        if (ClaimsSesion.ObtenerIdUsuario(principal) is not { } usuarioId)
        {
            return Results.Unauthorized();
        }

        var resultado = await anular.EjecutarAsync(id, peticion, usuarioId, cancelacion);

        return resultado.Exitoso
            ? Results.Ok(resultado.Viaje)
            : RespuestasDeViaje.TraducirFallo(resultado);
    }
}
