using System.Security.Claims;
using GT.Api.Autenticacion;
using GT.Api.Autorizacion;
using GT.Application.Facturacion;
using GT.Domain.Usuarios;

namespace GT.Api.Facturacion;

/// <summary>
/// Los dos cambios de estado de la factura: el cobro y la anulación (FR-042, FR-046).
///
/// <b>Cada uno es un recurso propio y nunca un campo del <c>PUT</c></b> (FR-044): así corregir un CAE mal
/// tipeado no puede marcar la factura como cobrada ni anularla en silencio. Es el precedente [004]
/// —cambiar el estado de una entidad es un recurso propio— aplicado a un ciclo de vida completo.
///
/// <b>Los dos exigen permisos distintos</b>, y es la única vez en el sistema que dos operaciones del
/// mismo grupo lo hacen: el cobro va con <c>facturacion.gestionar</c> y la anulación con
/// <c>facturacion.anular</c>, que otorga sólo el Administrador del sistema. Anular devuelve viajes a
/// <c>rendido</c> y no se deshace (FR-067, research §7).
/// </summary>
public static class CicloDeVidaFacturaEndpoints
{
    public static void MapearCicloDeVidaDeFacturas(this IEndpointRouteBuilder rutas)
    {
        var grupo = rutas.MapGroup("/api/facturas");

        grupo.MapPost("/{id:int}/cobro", RegistrarCobroAsync)
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.FacturacionGestionar));

        grupo.MapPost("/{id:int}/anulacion", AnularAsync)
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.FacturacionAnular));
    }

    /// <summary>
    /// Deja la factura en <c>pagada</c> con su fecha de cobro. <b><c>pagada</c> es terminal</b>: no existe
    /// ningún endpoint que revierta el cobro, y no está oculto — no existe (FR-043).
    /// </summary>
    private static async Task<IResult> RegistrarCobroAsync(
        int id,
        CobroRequest? peticion,
        ClaimsPrincipal principal,
        RegistrarCobro registrar,
        CancellationToken cancelacion)
    {
        if (ClaimsSesion.ObtenerIdUsuario(principal) is not { } usuarioId)
        {
            return Results.Unauthorized();
        }

        var resultado = await registrar.EjecutarAsync(id, peticion, usuarioId, cancelacion);

        return resultado.Exitoso
            ? Results.Ok(resultado.Factura)
            : RespuestasDeFactura.TraducirFallo(resultado);
    }

    /// <param name="peticion">
    /// El motivo es <b>obligatorio</b> (FR-046). La confirmación explícita la pide la pantalla, que no
    /// habilita el botón sin motivo escrito; el endpoint verifica el motivo igual.
    /// </param>
    private static async Task<IResult> AnularAsync(
        int id,
        AnulacionFacturaRequest? peticion,
        ClaimsPrincipal principal,
        AnularFactura anular,
        CancellationToken cancelacion)
    {
        if (ClaimsSesion.ObtenerIdUsuario(principal) is not { } usuarioId)
        {
            return Results.Unauthorized();
        }

        var resultado = await anular.EjecutarAsync(id, peticion, usuarioId, cancelacion);

        return resultado.Exitoso
            ? Results.Ok(resultado.Factura)
            : RespuestasDeFactura.TraducirFallo(resultado);
    }
}
