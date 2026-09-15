using System.Security.Claims;
using GT.Api.Autenticacion;
using GT.Api.Autorizacion;
using GT.Application.Adelantos;
using GT.Domain.Usuarios;

namespace GT.Api.Adelantos;

/// <summary>
/// Beneficiarios, personas del filtro, listado, detalle y registro.
///
/// <b>La ruta con identificador lleva <c>{id:int}</c>, y no es decorativo</b>: en el mismo prefijo conviven
/// <c>/beneficiarios</c> y <c>/personas</c>, que sin la restricción quedarían inalcanzables sin fallar al
/// compilar ni al arrancar (convención [005]).
///
/// El usuario de la sesión se lee acá y viaja <b>por parámetro</b> al caso de uso.
/// </summary>
public static class AdelantosEndpoints
{
    public static void MapearAdelantos(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/adelantos");

        var consultar = PoliticasAutorizacion.Para(CodigosPermiso.AdelantosConsultar);
        var gestionar = PoliticasAutorizacion.Para(CodigosPermiso.AdelantosGestionar);

        // `/beneficiarios` exige gestionar porque sólo lo usa el registro; `/personas`, consultar, porque lo
        // usa el filtro del listado que Gerencia ve (research §8).
        grupo.MapGet("/beneficiarios", ListarBeneficiariosAsync).RequireAuthorization(gestionar);
        grupo.MapGet("/personas", ListarPersonasAsync).RequireAuthorization(consultar);
        grupo.MapGet("/", ListarAsync).RequireAuthorization(consultar);
        grupo.MapGet("/{id:int}", ObtenerAsync).RequireAuthorization(consultar);
        grupo.MapPost("/", RegistrarAsync).RequireAuthorization(gestionar);
    }

    private static async Task<IResult> ListarBeneficiariosAsync(
        string? tipo,
        ConsultarBeneficiarios consultar,
        CancellationToken cancelacion)
    {
        if (NombresDeEstadoAdelanto.LeerTipo(tipo) is not { } tipoLeido)
        {
            return RespuestasDeAdelanto.TraducirFallo(ResultadoAdelanto.Rechazo(
                ErrorAdelanto.DatosInvalidos,
                MensajesAdelantos.TipoRequerido,
                "tipo"));
        }

        return Results.Ok(await consultar.EjecutarAsync(tipoLeido, cancelacion));
    }

    private static async Task<IResult> ListarPersonasAsync(
        ConsultarPersonasConAdelantos consultar,
        CancellationToken cancelacion) =>
        Results.Ok(await consultar.EjecutarAsync(cancelacion));

    /// <param name="estado">
    /// Sin él, <b>todos, incluidos rechazados y anulados</b>. Un valor desconocido se ignora (convención [003]).
    /// </param>
    private static async Task<IResult> ListarAsync(
        int? personaId,
        DateOnly? desde,
        DateOnly? hasta,
        string? estado,
        int? pagina,
        ConsultarAdelantos consultar,
        CancellationToken cancelacion)
    {
        var filtros = new FiltrosDeAdelantos(
            personaId,
            desde,
            hasta,
            NombresDeEstadoAdelanto.LeerEstado(estado),
            Math.Max(pagina ?? 1, 1));

        var resultado = await consultar.EjecutarAsync(filtros, cancelacion);

        return resultado.Rechazo is { } rechazo
            ? RespuestasDeAdelanto.TraducirFallo(rechazo)
            : Results.Ok(resultado.Pagina);
    }

    private static async Task<IResult> ObtenerAsync(
        int id,
        ConsultarDetalleAdelanto consultar,
        CancellationToken cancelacion)
    {
        var detalle = await consultar.EjecutarAsync(id, cancelacion);

        return detalle is not null ? Results.Ok(detalle) : RespuestasDeAdelanto.NoEncontrado();
    }

    private static async Task<IResult> RegistrarAsync(
        RegistroRequest? peticion,
        ClaimsPrincipal principal,
        RegistrarAdelanto registrar,
        CancellationToken cancelacion)
    {
        if (ClaimsSesion.ObtenerIdUsuario(principal) is not { } usuarioId)
        {
            return Results.Unauthorized();
        }

        var resultado = await registrar.EjecutarAsync(peticion, usuarioId, cancelacion);

        return resultado.Exitoso
            ? Results.Created($"/api/adelantos/{resultado.Adelanto!.Id}", resultado.Adelanto)
            : RespuestasDeAdelanto.TraducirFallo(resultado);
    }
}
