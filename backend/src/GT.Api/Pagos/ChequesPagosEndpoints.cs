using GT.Api.Autorizacion;
using GT.Api.Autenticacion;
using GT.Application.Pagos;
using GT.Domain.Usuarios;

namespace GT.Api.Pagos;

public static class ChequesPagosEndpoints
{
    public static void MapearChequesPagos(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/pagos/cheques/{id:int}/estado", (int id, HttpContext http, CambiarEstadoChequePago cambiar, CancellationToken ct) =>
            SerializadorPagos.EjecutarAsync(async () =>
            {
                var entrada = await SerializadorPagos.LeerAsync<EstadoChequeEntrada>(http.Request, ct);
                var detalle = await cambiar.EjecutarAsync(id, entrada, ClaimsSesion.ObtenerIdUsuario(http.User)!.Value, ct);
                return detalle is not null ? SerializadorPagos.Json(detalle)
                    : SerializadorPagos.Error(new ReglaPagoException("cheque_no_encontrado", "No encontramos ese cheque.", 404));
            })).RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.PagosChequesCambiarEstado));
        var consultas = app.MapGroup("/api/pagos/cheques")
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.PagosChequesConsultar));
        consultas.MapGet("/bancos", (IRepositorioPagos repo, CancellationToken ct) =>
            SerializadorPagos.EjecutarAsync(async () => SerializadorPagos.Json(new { items = await repo.BancosHistoricosAsync(ct) })));
        consultas.MapGet("/{id:int}", (int id, ConsultarChequesPagos consultar, CancellationToken ct) =>
            SerializadorPagos.EjecutarAsync(async () => await consultar.DetalleAsync(id, ct) is { } cheque
                ? SerializadorPagos.Json(cheque)
                : SerializadorPagos.Error(new ReglaPagoException("cheque_no_encontrado", "No encontramos ese cheque.", 404))));
        consultas.MapGet("", (HttpRequest request, ConsultarChequesPagos consultar, CancellationToken ct) =>
            SerializadorPagos.EjecutarAsync(async () => SerializadorPagos.Json(await consultar.ListarAsync(new(
                ParametrosConsultaPagos.Entero(request.Query, "bancoId"), ParametrosConsultaPagos.EstadoDelCheque(request.Query),
                ParametrosConsultaPagos.Fecha(request.Query, "desdeVencimiento"), ParametrosConsultaPagos.Fecha(request.Query, "hastaVencimiento"),
                ParametrosConsultaPagos.Entero(request.Query, "pagina") ?? 1), ct))));
    }
}
