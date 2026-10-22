using GT.Application.Choferes;
using GT.Domain.Choferes;
using GT.Domain.Pagos;

namespace GT.Application.Pagos;

public class ConsultarOrdenesProveedor(IRepositorioPagos pagos, TimeProvider reloj)
{
    public Task<PaginaDe<OrdenResumen>> ListarAsync(FiltroOrdenes filtro, CancellationToken ct = default) => pagos.OrdenesAsync(filtro, ct);
    public Task<DetalleOrden?> DetalleAsync(int id, CancellationToken ct = default) =>
        pagos.OrdenAsync(id, FechaHoyArgentina.Desde(reloj.GetUtcNow()), ct);
    public static void Validar(FiltroOrdenes filtro)
    {
        if (filtro.Desde > filtro.Hasta) throw new ReglaPagoException("rango_invalido", "Desde no puede ser posterior a Hasta.", campo: "desde");
        if (filtro.Pagina < 1 || filtro.Pagina > int.MaxValue / 20)
            throw new ReglaPagoException("pagina_invalida", "La página solicitada no es válida.", campo: "pagina");
        if (filtro.ProveedorId is <= 0) throw new ReglaPagoException("proveedor_invalido", "Elegí un proveedor válido.", campo: "proveedorId");
        if (filtro.Estado.HasValue && !Enum.IsDefined(filtro.Estado.Value))
            throw new ReglaPagoException("estado_invalido", "Elegí Pendiente o Anulada.", campo: "estado");
    }
}
