using GT.Api.Autenticacion;
using GT.Api.Autorizacion;
using GT.Application.Pagos;
using GT.Domain.Usuarios;

namespace GT.Api.Pagos;

public static class OrdenesPagosEndpoints
{
    public static void MapearOrdenesPagos(this IEndpointRouteBuilder app)
    {
        var consultas = app.MapGroup("/api/pagos/ordenes")
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.PagosConsultar));
        consultas.MapGet("", (HttpRequest request, ConsultarOrdenesProveedor consultar, CancellationToken ct) =>
            SerializadorPagos.EjecutarAsync(async () => SerializadorPagos.Json(await consultar.ListarAsync(new(
                ParametrosConsultaPagos.Fecha(request.Query, "desde"), ParametrosConsultaPagos.Fecha(request.Query, "hasta"),
                ParametrosConsultaPagos.Entero(request.Query, "proveedorId"), ParametrosConsultaPagos.EstadoOrden(request.Query),
                ParametrosConsultaPagos.Entero(request.Query, "pagina") ?? 1), ct))));
        consultas.MapGet("/proveedores", (HttpRequest request, IRepositorioPagos repo, CancellationToken ct) =>
            SerializadorPagos.EjecutarAsync(async () => SerializadorPagos.Json(await repo.ProveedoresHistoricosAsync(
                request.Query["q"].ToString(), ParametrosConsultaPagos.Entero(request.Query, "pagina") ?? 1, ct))));
        consultas.MapGet("/{id:int}", (int id, ConsultarOrdenesProveedor consultar, CancellationToken ct) =>
            SerializadorPagos.EjecutarAsync(async () => await consultar.DetalleAsync(id, ct) is { } orden
                ? SerializadorPagos.Json(orden)
                : SerializadorPagos.Error(new ReglaPagoException("orden_no_encontrada", "No encontramos esa orden de pago de proveedor.", 404))));
        app.MapPost("/api/pagos/ordenes/{id:int}/anulacion", (int id, HttpContext http, AnularOrdenProveedor anular, CancellationToken ct) =>
            SerializadorPagos.EjecutarAsync(async () =>
            {
                var entrada = await SerializadorPagos.LeerAsync<AnularOrdenEntrada>(http.Request, ct);
                var resultado = await anular.EjecutarAsync(id, entrada, ClaimsSesion.ObtenerIdUsuario(http.User)!.Value,
                    ClaimsSesion.Tiene(http.User, CodigosPermiso.PagosConsultar), ct);
                return SerializadorPagos.Escritura(resultado, http.Response);
            })).RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.PagosAnular));
        app.MapPost("/api/pagos/ordenes", (HttpContext http, GenerarOrdenProveedor generar, CancellationToken ct) =>
            SerializadorPagos.EjecutarAsync(async () =>
            {
                var entrada = await SerializadorPagos.LeerAsync<GenerarOrdenEntrada>(http.Request, ct);
                var resultado = await generar.EjecutarAsync(entrada, ClaimsSesion.ObtenerIdUsuario(http.User)!.Value,
                    ClaimsSesion.Tiene(http.User, CodigosPermiso.PagosConsultar), ct);
                return SerializadorPagos.Escritura(resultado, http.Response);
            })).RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.PagosGenerar));
    }
}
