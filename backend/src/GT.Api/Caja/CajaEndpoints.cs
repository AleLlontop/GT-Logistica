using System.Security.Claims;
using GT.Api.Autenticacion;
using GT.Api.Autorizacion;
using GT.Application.Caja;
using GT.Domain.Usuarios;

namespace GT.Api.Caja;

/// <summary>
/// La caja abierta propia, listado, detalle, apertura y los dos desplegables de referencia.
///
/// <b>La ruta con identificador lleva <c>{id:int}</c>, y no es decorativo</b>: en el mismo prefijo conviven
/// <c>/abierta</c>, <c>/facturas-pendientes</c> y <c>/ordenes-de-pago</c>, que sin la restricción quedarían
/// inalcanzables sin fallar al compilar ni al arrancar (convención [005]).
///
/// El usuario de la sesión se lee acá y viaja <b>por parámetro</b> al caso de uso.
/// </summary>
public static class CajaEndpoints
{
    public static void MapearCaja(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/caja");

        var consultar = PoliticasAutorizacion.Para(CodigosPermiso.CajaConsultar);
        var gestionar = PoliticasAutorizacion.Para(CodigosPermiso.CajaGestionar);

        // `/abierta` exige gestionar: sólo sirve para decidir *Abrir caja*, que Gerencia no ve.
        grupo.MapGet("/abierta", ObtenerAbiertaAsync).RequireAuthorization(gestionar);
        grupo.MapGet("/facturas-pendientes", ListarFacturasPendientesAsync).RequireAuthorization(gestionar);
        grupo.MapGet("/ordenes-de-pago", ListarOrdenesDePagoAsync).RequireAuthorization(gestionar);
        grupo.MapGet("/", ListarAsync).RequireAuthorization(consultar);
        grupo.MapGet("/{id:int}", ObtenerAsync).RequireAuthorization(consultar);
        grupo.MapPost("/", AbrirAsync).RequireAuthorization(gestionar);
    }

    /// <summary><c>204</c> sin cuerpo si el usuario no tiene ninguna abierta.</summary>
    private static async Task<IResult> ObtenerAbiertaAsync(
        ClaimsPrincipal principal,
        ConsultarDetalleCaja consultar,
        CancellationToken cancelacion)
    {
        if (ClaimsSesion.ObtenerIdUsuario(principal) is not { } usuarioId)
        {
            return Results.Unauthorized();
        }

        var abierta = await consultar.AbiertaDeAsync(usuarioId, cancelacion);

        return abierta is not null ? Results.Ok(abierta) : Results.NoContent();
    }

    private static async Task<IResult> ListarFacturasPendientesAsync(
        ConsultarFacturasPendientes consultar,
        CancellationToken cancelacion) =>
        Results.Ok(await consultar.EjecutarAsync(cancelacion));

    private static async Task<IResult> ListarOrdenesDePagoAsync(
        ConsultarOrdenesDePago consultar,
        CancellationToken cancelacion) =>
        Results.Ok(await consultar.EjecutarAsync(cancelacion));

    private static async Task<IResult> ListarAsync(
        int? pagina,
        ConsultarCajas consultar,
        CancellationToken cancelacion) =>
        Results.Ok(await consultar.EjecutarAsync(pagina ?? 1, cancelacion));

    private static async Task<IResult> ObtenerAsync(
        int id,
        ClaimsPrincipal principal,
        ConsultarDetalleCaja consultar,
        CancellationToken cancelacion)
    {
        if (ClaimsSesion.ObtenerIdUsuario(principal) is not { } usuarioId)
        {
            return Results.Unauthorized();
        }

        var detalle = await consultar.EjecutarAsync(id, usuarioId, cancelacion);

        return detalle is not null ? Results.Ok(detalle) : RespuestasDeCaja.NoEncontrada();
    }

    private static async Task<IResult> AbrirAsync(
        AbrirCajaRequest? peticion,
        ClaimsPrincipal principal,
        AbrirCaja abrir,
        CancellationToken cancelacion)
    {
        if (ClaimsSesion.ObtenerIdUsuario(principal) is not { } usuarioId)
        {
            return Results.Unauthorized();
        }

        var resultado = await abrir.EjecutarAsync(peticion, usuarioId, cancelacion);

        return resultado.Exitoso
            ? Results.Created($"/api/caja/{resultado.Caja!.Id}", resultado.Caja)
            : RespuestasDeCaja.TraducirFallo(resultado);
    }
}
