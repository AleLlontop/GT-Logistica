using GT.Api.Autenticacion;
using GT.Api.Autorizacion;
using GT.Application.Pagos;
using GT.Domain.Choferes;
using GT.Domain.Usuarios;

namespace GT.Api.Pagos;

public static class ReferenciasPagosEndpoints
{
    public static void MapearReferenciasPagos(this IEndpointRouteBuilder app)
    {
        var grupo = app.MapGroup("/api/pagos/referencias")
            .RequireAuthorization(PoliticasAutorizacion.Para(CodigosPermiso.PagosGenerar));
        grupo.MapGet("", (HttpContext http, IRepositorioPagos repo, TimeProvider reloj, CancellationToken ct) =>
            SerializadorPagos.EjecutarAsync(async () => SerializadorPagos.Json(await repo.ReferenciasAsync(
                ClaimsSesion.ObtenerIdUsuario(http.User)!.Value, FechaHoyArgentina.Desde(reloj.GetUtcNow()), ct))));
        grupo.MapGet("/facturas", (string? ids, IRepositorioPagos repo, CancellationToken ct) =>
            SerializadorPagos.EjecutarAsync(async () =>
            {
                var partes = ids?.Split(',') ?? [];
                var seleccion = new List<int>();
                foreach (var parte in partes)
                {
                    if (!int.TryParse(parte, System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out var id) || id <= 0)
                        throw new ReglaPagoException("facturas_invalidas", "Elegí facturas distintas y válidas.", campo: "facturaIds");
                    seleccion.Add(id);
                }
                return SerializadorPagos.Json(await repo.SeleccionAsync(seleccion.ToArray(), ct));
            }));
        grupo.MapGet("/proveedores", (string? q, int? pagina, IRepositorioPagos repo, CancellationToken ct) =>
            SerializadorPagos.EjecutarAsync(async () => SerializadorPagos.Json(await repo.ProveedoresElegiblesAsync(q, pagina ?? 1, ct))));
        grupo.MapGet("/proveedores/{id:int}/facturas", (int id, int? pagina, IRepositorioPagos repo, CancellationToken ct) =>
            SerializadorPagos.EjecutarAsync(async () => SerializadorPagos.Json(await repo.FacturasElegiblesAsync(id, pagina ?? 1, ct))));
    }
}
