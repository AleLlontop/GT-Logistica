using System.Security.Claims;
using GT.Api.Autenticacion;
using GT.Api.Autorizacion;
using GT.Application.Caja;
using GT.Domain.Usuarios;

namespace GT.Api.Caja;

/// <summary>
/// Registrar un movimiento, los movimientos de una caja y la consulta global por rango y por caja. No hay
/// <c>PUT</c> ni <c>DELETE</c>: un movimiento no se edita, no se anula, no se borra (FR-014).
/// </summary>
public static class MovimientosDeCajaEndpoints
{
    public static void MapearMovimientosDeCaja(this IEndpointRouteBuilder rutas)
    {
        var consultar = PoliticasAutorizacion.Para(CodigosPermiso.CajaConsultar);
        var gestionar = PoliticasAutorizacion.Para(CodigosPermiso.CajaGestionar);

        var caja = rutas.MapGroup("/api/caja");

        caja.MapPost("/{id:int}/movimientos", RegistrarAsync).RequireAuthorization(gestionar);
        caja.MapGet("/{id:int}/movimientos", ListarDeCajaAsync).RequireAuthorization(consultar);

        rutas.MapGet("/api/movimientos-caja", ListarAsync).RequireAuthorization(consultar);
    }

    private static async Task<IResult> RegistrarAsync(
        int id,
        RegistrarMovimientoRequest? peticion,
        ClaimsPrincipal principal,
        RegistrarMovimiento registrar,
        CancellationToken cancelacion)
    {
        if (ClaimsSesion.ObtenerIdUsuario(principal) is not { } usuarioId)
        {
            return Results.Unauthorized();
        }

        var resultado = await registrar.EjecutarAsync(id, peticion, usuarioId, cancelacion);

        return resultado.Exitoso
            ? Results.Created($"/api/caja/{id}/movimientos", resultado.Movimiento)
            : RespuestasDeCaja.TraducirFallo(resultado);
    }

    private static async Task<IResult> ListarDeCajaAsync(
        int id,
        int? pagina,
        ClaimsPrincipal principal,
        ConsultarMovimientosDeCaja consultar,
        CancellationToken cancelacion)
    {
        if (ClaimsSesion.ObtenerIdUsuario(principal) is not { } usuarioId)
        {
            return Results.Unauthorized();
        }

        var movimientos = await consultar.EjecutarAsync(id, pagina ?? 1, usuarioId, cancelacion);

        return movimientos is not null ? Results.Ok(movimientos) : RespuestasDeCaja.NoEncontrada();
    }

    /// <param name="desde">Día de Argentina, incluido.</param>
    /// <param name="hasta">Día de Argentina, incluido entero (tasks.md decisión 4).</param>
    private static async Task<IResult> ListarAsync(
        DateOnly? desde,
        DateOnly? hasta,
        int? cajaId,
        int? pagina,
        ConsultarMovimientos consultar,
        CancellationToken cancelacion)
    {
        var resultado = await consultar.EjecutarAsync(
            new FiltrosDeMovimientos(desde, hasta, cajaId, Math.Max(pagina ?? 1, 1)),
            cancelacion: cancelacion);

        return resultado.Rechazo is { } rechazo
            ? RespuestasDeCaja.TraducirFallo(rechazo)
            : Results.Ok(resultado.Pagina);
    }
}
