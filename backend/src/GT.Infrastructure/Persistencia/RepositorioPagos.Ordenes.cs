using GT.Application.Choferes;
using GT.Application.Pagos;
using GT.Domain.Pagos;
using GT.Domain.Proveedores;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioPagos
{
    public async Task<PaginaDe<OrdenResumen>> OrdenesAsync(FiltroOrdenes filtro, CancellationToken ct = default)
    {
        ConsultarOrdenesProveedor.Validar(filtro);
        var consulta = contexto.OrdenesPagoProveedor.AsNoTracking().AsQueryable();
        if (filtro.Desde is { } desde) consulta = consulta.Where(o => o.Fecha >= desde);
        if (filtro.Hasta is { } hasta) consulta = consulta.Where(o => o.Fecha <= hasta);
        if (filtro.ProveedorId is { } proveedor) consulta = consulta.Where(o => o.ProveedorId == proveedor);
        if (filtro.Estado is { } estado) consulta = consulta.Where(o => o.Estado == estado);
        var total = await consulta.CountAsync(ct);
        var items = await consulta.OrderByDescending(o => o.Fecha).ThenByDescending(o => o.Id).Skip((filtro.Pagina - 1) * 20).Take(20)
            .Select(o => new OrdenResumen(o.Id, NumeroVisiblePagoProveedor.Desde(o.Numero), o.Fecha,
                new(o.ProveedorId, o.ProveedorRazonSocial, o.ProveedorCuit, o.TipoProveedorAlRegistrar, o.ProveedorActivoAlGenerar, o.Proveedor!.Activo),
                o.ImporteTotal, o.Estado)).ToListAsync(ct);
        return new(items, total, filtro.Pagina, 20);
    }

    public async Task<PaginaDe<ProveedorOpcion>> ProveedoresHistoricosAsync(string? q, int pagina, CancellationToken ct = default)
    {
        ElegibilidadPagosSql.ValidarPagina(pagina);
        var consulta = contexto.Proveedores.AsNoTracking().Where(p => contexto.OrdenesPagoProveedor.Any(o => o.ProveedorId == p.Id))
            .Select(p => new { p.Id, RazonSocial = p.Tipo == TipoProveedor.Fletero ? p.Transportista!.Nombre : p.RazonSocial!,
                Cuit = p.Tipo == TipoProveedor.Fletero ? p.Transportista!.Cuit : p.Cuit!, p.Tipo, p.Activo });
        if (!string.IsNullOrWhiteSpace(q)) { var texto = q.Trim(); consulta = consulta.Where(p => p.RazonSocial.Contains(texto) || p.Cuit.Contains(texto)); }
        var total = await consulta.CountAsync(ct);
        var items = await consulta.OrderBy(p => p.RazonSocial).ThenBy(p => p.Id).Skip((pagina - 1) * 20).Take(20)
            .Select(p => new ProveedorOpcion(p.Id, p.RazonSocial, p.Cuit, p.Tipo, p.Activo)).ToListAsync(ct);
        return new(items, total, pagina, 20);
    }
}
