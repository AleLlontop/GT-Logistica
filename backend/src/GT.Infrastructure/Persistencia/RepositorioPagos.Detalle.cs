using GT.Application.Pagos;
using GT.Domain.Caja;
using GT.Domain.Liquidaciones;
using GT.Domain.Pagos;
using GT.Domain.Proveedores;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioPagos
{
    public async Task<DetalleOrden?> OrdenAsync(int id, DateOnly hoy, CancellationToken ct = default)
    {
        var orden = await contexto.OrdenesPagoProveedor.AsNoTracking()
            .Include(x => x.Proveedor).Include(x => x.Usuario).Include(x => x.AnuladaPor)
            .Include(x => x.Facturas).ThenInclude(x => x.FacturaProveedor).ThenInclude(x => x!.Liquidacion)
            .Include(x => x.Medios).AsSplitQuery().SingleOrDefaultAsync(x => x.Id == id, ct);
        if (orden is null) return null;
        var pagos = await contexto.OrdenesDePago.AsNoTracking().Where(x => x.OrdenPagoProveedorId == id).ToListAsync(ct);
        var bancosIds = orden.Medios.OfType<Cheque>().Select(x => x.BancoId).ToArray();
        var bancos = await contexto.Bancos.AsNoTracking().Where(x => bancosIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var movimientos = await contexto.MovimientosDeCaja.AsNoTracking().Include(x => x.Usuario)
            .Where(x => x.OrdenPagoProveedorId == id).OrderBy(x => x.Id).ToListAsync(ct);
        var numero = NumeroVisiblePagoProveedor.Desde(orden.Numero);
        MovimientoReferencia Movimiento(MovimientoDeCaja m) => new(m.Id, m.CajaId,
            m.Tipo == TipoMovimientoCaja.Ingreso ? "ingreso" : "egreso", m.Importe, m.Concepto,
            new(m.UsuarioId, m.Usuario!.Username), m.Fecha, new(id, numero));
        var egreso = movimientos.SingleOrDefault(x => x.Tipo == TipoMovimientoCaja.Egreso);
        var ingreso = movimientos.SingleOrDefault(x => x.Tipo == TipoMovimientoCaja.Ingreso);
        var facturas = orden.Facturas.OrderBy(x => x.FacturaProveedorId).Select(v =>
        {
            var f = v.FacturaProveedor!;
            LiquidacionEnOrden? liquidacion = null;
            if (f.Liquidacion is { } l)
            {
                var pago = pagos.Single(x => x.LiquidacionId == l.Id);
                liquidacion = new(l.Id, NumerosVisibles.Liquidacion(l.Numero), f.ImporteLiquidacionAlRegistrar!.Value,
                    l.ImporteTotal, l.ImporteTotal - l.ImportePagado, l.Estado.ToString().ToLowerInvariant(),
                    new(pago.Id, NumerosVisibles.OrdenDePago(pago.Numero), pago.FechaPago, pago.Importe, pago.Vigente, pago.RevertidaEn));
            }
            return new FacturaEnOrden(f.Id, f.Numero, f.Fecha, v.ImporteTotalAlPagar,
                f.Estado == EstadoFacturaProveedor.Pagada ? "pagada" : "pendiente", v.Vigente,
                f.ProveedorRazonSocial, f.ProveedorCuit, liquidacion);
        }).ToArray();
        var medios = orden.Medios.OrderBy(x => x.RenglonId).Select(m => m switch
        {
            Cheque c => (MedioDetalle)new ChequeEnOrden(c.Id, c.RenglonId, c.Importe, c.Fecha, c.BancoId,
                c.NumeroCheque, c.FechaVencimiento, c.DetalleCheque, c.EstadoCheque, SituacionCheque(c.FechaVencimiento, hoy),
                new(c.BancoId, bancos[c.BancoId].Nombre, bancos[c.BancoId].Activo)),
            Transferencia t => new TransferenciaEnOrden(t.Id, t.RenglonId, t.Importe, t.Fecha, t.CbuOrigen,
                t.CbuDestino, t.NumeroComprobante, t.Observaciones),
            Efectivo e => new EfectivoEnOrden(e.Id, e.RenglonId, e.Importe, e.Fecha),
            _ => throw new InvalidOperationException("Tipo de medio desconocido."),
        }).ToArray();
        AnulacionDto? anulacion = orden.Estado != EstadoOrdenPagoProveedor.Anulada ? null : new(orden.MotivoAnulacion!,
            new(orden.AnuladaPorId!.Value, orden.AnuladaPor!.Username), orden.AnuladaEn!.Value,
            new(facturas.Select(x => x.Id).ToArray(), orden.Medios.OfType<Cheque>().Select(x => x.Id).ToArray(),
                pagos.Select(p => new ReferenciaPago(p.Id, NumerosVisibles.OrdenDePago(p.Numero))).ToArray(), ingreso is null ? null : Movimiento(ingreso)));
        return new(orden.Id, numero, orden.Fecha,
            new(orden.ProveedorId, orden.ProveedorRazonSocial, orden.ProveedorCuit, orden.TipoProveedorAlRegistrar,
                orden.ProveedorActivoAlGenerar, orden.Proveedor!.Activo), orden.ImporteTotal, orden.Estado, orden.Detalle,
            new(orden.UsuarioId, orden.Usuario!.Username), orden.RegistradaEn, facturas, medios,
            egreso is null ? null : new(egreso.CajaId, Movimiento(egreso), ingreso is null ? null : Movimiento(ingreso)), anulacion);
    }

    private static string SituacionCheque(DateOnly vencimiento, DateOnly hoy) =>
        NombresVencimientoCheque.EnJson(VencimientoCheque.Evaluar(vencimiento, hoy));
}
