using GT.Application.Choferes;
using GT.Application.Pagos;
using GT.Domain.Liquidaciones;
using GT.Domain.Proveedores;
using GT.Domain.Pagos;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

/// <summary>Un único predicado SQL para ofrecer y revalidar la selección; nunca filtra tras paginar.</summary>
public static class ElegibilidadPagosSql
{
    public static IQueryable<FacturaProveedor> Elegibles(GtDbContext db) =>
        db.FacturasProveedor.AsNoTracking().Where(f => f.Estado == EstadoFacturaProveedor.Pendiente
            && !db.OrdenPagoProveedorFacturas.Any(v => v.FacturaProveedorId == f.Id && v.Vigente)
            && (f.TipoProveedorAlRegistrar == TipoProveedor.ProveedorNormal
                || f.TipoProveedorAlRegistrar == TipoProveedor.Fletero && f.LiquidacionId != null
                && f.Liquidacion != null && f.Liquidacion.Estado == EstadoLiquidacion.Pendiente
                && f.Liquidacion.ImportePagado == 0m
                && !db.OrdenesDePago.Any(p => p.LiquidacionId == f.LiquidacionId && p.Vigente)
                && f.ImporteLiquidacionAlRegistrar != null
                && f.Liquidacion.ImporteTotal == f.ImporteLiquidacionAlRegistrar
                && f.Liquidacion.ImporteTotal - f.Liquidacion.ImportePagado == f.ImporteTotal));

    public static IQueryable<FacturaElegible> Proyectar(IQueryable<FacturaProveedor> consulta) =>
        consulta.Select(f => new FacturaElegible(f.Id, f.Numero, f.Fecha, f.ImporteTotal, f.ProveedorId,
            f.TipoProveedorAlRegistrar, f.Liquidacion == null ? null : new LiquidacionElegible(
                f.Liquidacion.Id, NumerosVisibles.Liquidacion(f.Liquidacion.Numero), f.ImporteLiquidacionAlRegistrar!.Value,
                f.Liquidacion.ImporteTotal, f.Liquidacion.ImporteTotal - f.Liquidacion.ImportePagado,
                f.Liquidacion.Estado == EstadoLiquidacion.Pendiente ? "pendiente" : f.Liquidacion.Estado == EstadoLiquidacion.Pagada ? "pagada" : "anulada")));

    public static async Task<PaginaDe<ProveedorOpcion>> ProveedoresAsync(GtDbContext db, string? q, int pagina, CancellationToken ct)
    {
        ValidarPagina(pagina);
        var consulta = Elegibles(db).Select(f => new
        {
            Id = f.ProveedorId,
            RazonSocial = f.Proveedor!.Tipo == TipoProveedor.Fletero ? f.Proveedor.Transportista!.Nombre : f.Proveedor.RazonSocial!,
            Cuit = f.Proveedor.Tipo == TipoProveedor.Fletero ? f.Proveedor.Transportista!.Cuit : f.Proveedor.Cuit!,
            f.Proveedor.Tipo,
            f.Proveedor.Activo,
        }).Distinct();
        if (!string.IsNullOrWhiteSpace(q))
        {
            q = q.Trim();
            consulta = consulta.Where(p => p.RazonSocial.Contains(q) || p.Cuit.Contains(q));
        }
        return new(await consulta.OrderBy(x => x.RazonSocial).ThenBy(x => x.Id).Skip((pagina - 1) * 20).Take(20)
            .Select(x => new ProveedorOpcion(x.Id, x.RazonSocial, x.Cuit, x.Tipo, x.Activo)).ToListAsync(ct),
            await consulta.CountAsync(ct), pagina, 20);
    }
    public static async Task<PaginaDe<FacturaElegible>> FacturasAsync(GtDbContext db, int proveedorId, int pagina, CancellationToken ct)
    {
        ValidarPagina(pagina);
        var consulta = Elegibles(db).Where(f => f.ProveedorId == proveedorId);
        return new(await Proyectar(consulta.OrderBy(f => f.Fecha).ThenBy(f => f.Id).Skip((pagina - 1) * 20).Take(20)).ToListAsync(ct),
            await consulta.CountAsync(ct), pagina, 20);
    }
    public static async Task<SeleccionFacturas> SeleccionAsync(GtDbContext db, int[] ids, CancellationToken ct)
    {
        if (ids.Length == 0 || ids.Any(i => i <= 0) || ids.Distinct().Count() != ids.Length)
            throw new ReglaPagoException("facturas_invalidas", "Elegí facturas distintas y válidas.", campo: "facturaIds");
        var items = await Proyectar(Elegibles(db).Where(f => ids.Contains(f.Id)).OrderBy(f => f.Id)).ToListAsync(ct);
        if (items.Select(x => x.ProveedorId).Distinct().Count() > 1)
            throw new ReglaPagoException("proveedores_distintos", "Todas las facturas deben pertenecer al mismo proveedor.", campo: "facturaIds");
        var total = 0m;
        if (items.Count > 0 && !ReglasDePagos.IntentarSumarImportes(items.Select(x => x.ImporteTotal), out total))
            throw new ReglaPagoException("total_fuera_de_capacidad", "El total de las facturas excede la capacidad.", campo: "facturaIds");
        return new(items, ids.Except(items.Select(x => x.Id)).Order().ToArray(), total);
    }
    public static void ValidarPagina(int pagina)
    {
        if (pagina < 1 || pagina > int.MaxValue / 20)
            throw new ReglaPagoException("pagina_invalida", "La página solicitada no es válida.", campo: "pagina");
    }
}
