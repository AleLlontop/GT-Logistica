using GT.Application.Pagos;
using GT.Domain.Caja;
using GT.Domain.Liquidaciones;
using GT.Domain.Pagos;
using GT.Domain.Proveedores;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public partial class RepositorioPagos
{
    public async Task<ResultadoEscrituraPago> AnularAsync(int id, AnularOrdenEntrada entrada, int usuarioId,
        bool puedeConsultar, DateOnly hoy, DateTime ahoraUtc, CancellationToken ct = default)
    {
        AnularOrdenProveedor.ValidarEntrada(entrada);
        var comando = CanonicalizadorPago.Anular(id, entrada);
        var commitIntentado = false;
        try
        {
            await using var tx = await contexto.Database.BeginTransactionAsync(ct);
            var locks = new BloqueosPagos(contexto);
            var resultados = new ResultadosIdempotentesPago(contexto);
            await locks.IdempotenciaAsync(TipoOperacionPago.Anular, entrada.ClaveOperacion, ct);
            var replay = await resultados.BuscarAsync(TipoOperacionPago.Anular, entrada.ClaveOperacion, comando, usuarioId, puedeConsultar, ct);
            if (replay is not null) return replay;
            if (await locks.OrdenAsync(id, ct) != 1) throw new ReglaPagoException("orden_no_encontrada", "No encontramos esa orden de pago de proveedor.", 404);
            var orden = await contexto.OrdenesPagoProveedor.AsNoTracking().SingleAsync(o => o.Id == id, ct);
            var vinculos = await contexto.OrdenPagoProveedorFacturas.AsNoTracking().Where(v => v.OrdenPagoProveedorId == id).OrderBy(v => v.FacturaProveedorId).ToListAsync(ct);
            await locks.FacturasAsync(vinculos.Select(v => v.FacturaProveedorId), false, ct);
            var pagos = await contexto.OrdenesDePago.AsNoTracking().Where(p => p.OrdenPagoProveedorId == id).OrderBy(p => p.Id).ToListAsync(ct);
            await locks.LiquidacionesAsync(pagos.Select(p => p.LiquidacionId), ct);
            await locks.PagosAsync(pagos.Select(p => p.Id), ct);
            var chequeIds = await contexto.Cheques.Where(c => c.OrdenPagoProveedorId == id).OrderBy(c => c.Id).Select(c => c.Id).ToListAsync(ct);
            await locks.ChequesAsync(chequeIds, ct);
            var egreso = await contexto.MovimientosDeCaja.AsNoTracking().SingleOrDefaultAsync(m => m.OrdenPagoProveedorId == id && m.Tipo == TipoMovimientoCaja.Egreso, ct);
            if (egreso is not null) await locks.CajaAsync(egreso.CajaId, null, ct);
            // Releer todos los datos cuyo estado pudo cambiar antes de adquirir sus locks.
            pagos = await contexto.OrdenesDePago.AsNoTracking().Where(p => p.OrdenPagoProveedorId == id).OrderBy(p => p.Id).ToListAsync(ct);
            var cheques = await contexto.Cheques.AsNoTracking().Where(c => c.OrdenPagoProveedorId == id).OrderBy(c => c.Id).ToListAsync(ct);
            var transferencia = await contexto.Transferencias.AnyAsync(t => t.OrdenPagoProveedorId == id, ct);
            var facturas = await contexto.FacturasProveedor.AsNoTracking().Where(f => vinculos.Select(v => v.FacturaProveedorId).Contains(f.Id)).ToListAsync(ct);
            var reversibles = pagos.All(p => p.Vigente && p.OrdenPagoProveedorId == id);
            foreach (var pago in pagos)
            {
                var liquidacion = await contexto.Liquidaciones.AsNoTracking().SingleAsync(l => l.Id == pago.LiquidacionId, ct);
                reversibles &= liquidacion.Estado == EstadoLiquidacion.Pagada && liquidacion.ImportePagado == pago.Importe
                    && liquidacion.ImporteTotal == pago.Importe;
            }
            if (orden.TipoProveedorAlRegistrar == TipoProveedor.Fletero)
                reversibles &= pagos.Count == facturas.Count && facturas.All(f => pagos.Any(p => p.LiquidacionId == f.LiquidacionId && p.Importe == f.ImporteTotal));
            AnularOrdenProveedor.ValidarSituacion(orden.Estado, cheques.Select(c => c.EstadoCheque), transferencia, true, reversibles);
            if (vinculos.Count == 0 || vinculos.Any(v => !v.Vigente) || facturas.Count != vinculos.Count || facturas.Any(f => f.Estado != EstadoFacturaProveedor.Pagada))
                throw new ReglaPagoException("orden_no_anulable", "Las facturas de la orden ya no están en la situación esperada.", 409);
            var efectivo = await contexto.Efectivos.Where(e => e.OrdenPagoProveedorId == id).SumAsync(e => (decimal?)e.Importe, ct) ?? 0m;
            if (efectivo > 0 && (egreso is null || egreso.Importe != efectivo))
                throw new ReglaPagoException("orden_no_anulable", "El egreso original no coincide con el efectivo de la orden.", 409);
            if (egreso is not null) await new PrimitivasMovimientosCaja(contexto).InsertarAsync(new()
            {
                CajaId = egreso.CajaId, Tipo = TipoMovimientoCaja.Ingreso, Importe = efectivo,
                Concepto = "Anulación " + NumeroVisiblePagoProveedor.Desde(orden.Numero), UsuarioId = usuarioId,
                Fecha = ahoraUtc, OrdenPagoProveedorId = id,
            }, ct);
            foreach (var pago in pagos) await new PrimitivasPagosLiquidacion(contexto).RevertirAsync(pago, id, usuarioId, ahoraUtc, ct);
            foreach (var vinculo in vinculos)
            {
                await contexto.FacturasProveedor.Where(f => f.Id == vinculo.FacturaProveedorId && f.Estado == EstadoFacturaProveedor.Pagada)
                    .ExecuteUpdateAsync(u => u.SetProperty(f => f.Estado, EstadoFacturaProveedor.Pendiente), ct);
                await contexto.OrdenPagoProveedorFacturas.Where(v => v.OrdenPagoProveedorId == id && v.FacturaProveedorId == vinculo.FacturaProveedorId && v.Vigente)
                    .ExecuteUpdateAsync(u => u.SetProperty(v => v.Vigente, false), ct);
            }
            foreach (var cheque in cheques)
            {
                if (await contexto.Cheques.Where(c => c.Id == cheque.Id && c.EstadoCheque == EstadoCheque.Pendiente)
                    .ExecuteUpdateAsync(u => u.SetProperty(c => c.EstadoCheque, EstadoCheque.Anulado), ct) != 1)
                    throw new ReglaPagoException("estado_cheque_modificado", "El estado del cheque cambió.", 409);
                contexto.HistorialEstadosCheque.Add(new CambioEstadoCheque { ChequeId = cheque.Id, EstadoAnterior = EstadoCheque.Pendiente,
                    EstadoNuevo = EstadoCheque.Anulado, UsuarioId = usuarioId, OcurridoEn = ahoraUtc });
            }
            if (await contexto.OrdenesPagoProveedor.Where(o => o.Id == id && o.Estado == EstadoOrdenPagoProveedor.Pendiente)
                .ExecuteUpdateAsync(u => u.SetProperty(o => o.Estado, EstadoOrdenPagoProveedor.Anulada)
                    .SetProperty(o => o.MotivoAnulacion, entrada.Motivo!.Trim()).SetProperty(o => o.AnuladaPorId, usuarioId)
                    .SetProperty(o => o.AnuladaEn, ahoraUtc), ct) != 1)
                throw new ReglaPagoException("orden_no_anulable", "La orden ya está Anulada.", 409);
            await contexto.SaveChangesAsync(ct);
            var detalle = await OrdenAsync(id, hoy, ct) ?? throw new InvalidOperationException("Falta la orden anulada.");
            var respuesta = new ResultadoEscrituraPago(200, serializador.Serializar(detalle), null);
            await resultados.GuardarAsync(TipoOperacionPago.Anular, entrada.ClaveOperacion, comando, usuarioId, id, respuesta, ahoraUtc, ct);
            commitIntentado = true;
            await tx.CommitAsync(ct);
            return respuesta;
        }
        catch (Exception e) when (commitIntentado && e is not ReglaPagoException)
        {
            contexto.ChangeTracker.Clear();
            return await ResultadosIdempotentesPago.RecuperarAsync(scopes, TipoOperacionPago.Anular, entrada.ClaveOperacion, comando, usuarioId, puedeConsultar, ct);
        }
        catch (Exception e) when (BloqueosPagos.Traducir(e) is not null)
        {
            contexto.ChangeTracker.Clear(); throw BloqueosPagos.Traducir(e)!;
        }
    }
}
