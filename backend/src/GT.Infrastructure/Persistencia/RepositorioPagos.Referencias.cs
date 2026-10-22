using GT.Application.Pagos;
using GT.Application.Choferes;
using GT.Domain.Caja;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioPagos
{
    public Task<PaginaDe<ProveedorOpcion>> ProveedoresElegiblesAsync(string? q, int pagina, CancellationToken ct = default) =>
        ElegibilidadPagosSql.ProveedoresAsync(contexto, q, pagina, ct);

    public Task<PaginaDe<FacturaElegible>> FacturasElegiblesAsync(int proveedorId, int pagina, CancellationToken ct = default)
    {
        if (proveedorId <= 0) throw new ReglaPagoException("proveedor_invalido", "Elegí un proveedor válido.", campo: "proveedorId");
        return ElegibilidadPagosSql.FacturasAsync(contexto, proveedorId, pagina, ct);
    }

    public Task<SeleccionFacturas> SeleccionAsync(int[] ids, CancellationToken ct = default) =>
        ElegibilidadPagosSql.SeleccionAsync(contexto, ids, ct);

    public async Task<ReferenciasPago> ReferenciasAsync(int usuarioId, DateOnly hoy, CancellationToken ct = default)
    {
        var caja = await contexto.Cajas.AsNoTracking().Where(c => c.UsuarioResponsableId == usuarioId && c.Estado == EstadoCaja.Abierta)
            .Select(c => new CajaReferencia(c.Id, "abierta", c.SaldoInicial +
                (contexto.MovimientosDeCaja.Where(m => m.CajaId == c.Id)
                    .Sum(m => (decimal?)(m.Tipo == TipoMovimientoCaja.Ingreso ? m.Importe : -m.Importe)) ?? 0m)))
            .SingleOrDefaultAsync(ct);
        var bancos = await contexto.Bancos.AsNoTracking().Where(b => b.Activo).OrderBy(b => b.Nombre).ThenBy(b => b.Id)
            .Select(b => new BancoReferencia(b.Id, b.Nombre, b.Activo)).ToListAsync(ct);
        return new(hoy, caja, bancos);
    }
}
