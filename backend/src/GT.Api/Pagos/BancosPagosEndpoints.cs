using GT.Api.Autorizacion;
using GT.Application.Pagos;
using GT.Domain.Usuarios;

namespace GT.Api.Pagos;

public static class BancosPagosEndpoints
{
    public static void MapearBancosPagos(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/pagos/bancos");
        grupo.MapGet("", (int? pagina, IRepositorioPagos repo, CancellationToken ct) =>
            SerializadorPagos.EjecutarAsync(async () => SerializadorPagos.Json(await repo.BancosAsync(pagina ?? 1, ct))))
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.PagosBancosConsultar));
        grupo.MapGet("/{id:int}", (int id, IRepositorioPagos repo, CancellationToken ct) =>
            SerializadorPagos.EjecutarAsync(async () => await repo.BancoAsync(id, ct) is { } banco
                ? SerializadorPagos.Json(banco) : NoEncontrado()))
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.PagosBancosConsultar));
        grupo.MapPost("", (HttpRequest request, HttpResponse response, GestionBancosPagos bancos, CancellationToken ct) =>
            SerializadorPagos.EjecutarAsync(async () =>
            {
                var entrada = await SerializadorPagos.LeerAsync<BancoAltaEntrada>(request, ct);
                var banco = await bancos.CrearAsync(new(entrada.Nombre), ct);
                response.Headers.Location = $"/api/pagos/bancos/{banco.Id}";
                return SerializadorPagos.Json(banco, 201);
            })).RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.PagosBancosGestionar));
        grupo.MapPut("/{id:int}", (int id, HttpRequest request, GestionBancosPagos bancos, CancellationToken ct) =>
            SerializadorPagos.EjecutarAsync(async () =>
            {
                var entrada = await SerializadorPagos.LeerAsync<BancoEdicionEntrada>(request, ct);
                return await bancos.EditarAsync(id, new(entrada.Nombre, entrada.Version), ct) is { } banco ? SerializadorPagos.Json(banco) : NoEncontrado();
            })).RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.PagosBancosGestionar));
        grupo.MapPut("/{id:int}/activacion", (int id, IRepositorioPagos repo, CancellationToken ct) =>
            SerializadorPagos.EjecutarAsync(async () => await repo.ActivarBancoAsync(id, true, ct) is { } banco
                ? SerializadorPagos.Json(banco) : NoEncontrado()))
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.PagosBancosGestionar));
        grupo.MapPut("/{id:int}/desactivacion", (int id, IRepositorioPagos repo, CancellationToken ct) =>
            SerializadorPagos.EjecutarAsync(async () => await repo.ActivarBancoAsync(id, false, ct) is { } banco
                ? SerializadorPagos.Json(banco) : NoEncontrado()))
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.PagosBancosGestionar));
    }
    public static IResult NoEncontrado() => SerializadorPagos.Json(new { codigo = "banco_no_encontrado", mensaje = "No se encontró el banco." }, 404);
}
