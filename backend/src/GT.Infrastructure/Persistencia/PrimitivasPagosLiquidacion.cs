using GT.Domain.Liquidaciones;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

/// <summary>Escrituras internas caller-owned. No abren ni confirman transacciones.</summary>
public class PrimitivasPagosLiquidacion(GtDbContext contexto)
{
    private void ExigirTransaccion()
    {
        if (contexto.Database.CurrentTransaction is null)
            throw new InvalidOperationException("La operación de liquidación requiere una transacción del llamador.");
    }

    public Task<int> TomarAsync(int id, CancellationToken ct = default)
    {
        ExigirTransaccion();
        return contexto.Liquidaciones.Where(x => x.Id == id)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Estado, x => x.Estado), ct);
    }

    public async Task<OrdenDePago?> RegistrarAsync(int id, DateOnly fecha, decimal importe, int usuarioId,
        DateTime ahoraUtc, int? ordenPagoProveedorId = null, CancellationToken ct = default)
    {
        ExigirTransaccion();
        var afectadas = await contexto.Liquidaciones
            .Where(x => x.Id == id && x.Estado == EstadoLiquidacion.Pendiente && x.ImportePagado + importe <= x.ImporteTotal)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.ImportePagado, x => x.ImportePagado + importe)
                .SetProperty(x => x.Estado, x => x.ImportePagado + importe == x.ImporteTotal ? EstadoLiquidacion.Pagada : EstadoLiquidacion.Pendiente), ct);
        if (afectadas != 1) return null;
        var orden = new OrdenDePago { LiquidacionId = id, FechaPago = fecha, Importe = importe,
            UsuarioId = usuarioId, RegistradaEn = ahoraUtc, OrdenPagoProveedorId = ordenPagoProveedorId };
        contexto.OrdenesDePago.Add(orden);
        var estado = await contexto.Liquidaciones.Where(x => x.Id == id).Select(x => x.Estado).SingleAsync(ct);
        if (ordenPagoProveedorId.HasValue || estado == EstadoLiquidacion.Pagada)
            contexto.CambiosDeLiquidacion.Add(new CambioDeLiquidacion { LiquidacionId = id,
                Operacion = ordenPagoProveedorId.HasValue ? OperacionDeLiquidacion.PagoPorOPP : OperacionDeLiquidacion.Pagada,
                OrdenDePago = ordenPagoProveedorId.HasValue ? orden : null,
                UsuarioId = usuarioId, OcurridoEn = ahoraUtc });
        await contexto.SaveChangesAsync(ct);
        return orden;
    }

    /// <summary>El caller ya tomó todas las liquidaciones y OP en el orden global antes de escribir.</summary>
    public async Task RevertirAsync(OrdenDePago orden, int oppId, int usuarioId, DateTime ahoraUtc, CancellationToken ct = default)
    {
        ExigirTransaccion();
        var afectadas = await contexto.OrdenesDePago.Where(x => x.Id == orden.Id && x.OrdenPagoProveedorId == oppId && x.Vigente)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Vigente, false).SetProperty(x => x.RevertidaPorId, usuarioId)
                .SetProperty(x => x.RevertidaEn, ahoraUtc), ct);
        if (afectadas != 1) throw new InvalidOperationException("El pago propio ya no está vigente.");
        afectadas = await contexto.Liquidaciones.Where(x => x.Id == orden.LiquidacionId && x.ImportePagado >= orden.Importe && x.Estado != EstadoLiquidacion.Anulada)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.ImportePagado, x => x.ImportePagado - orden.Importe)
                .SetProperty(x => x.Estado, EstadoLiquidacion.Pendiente), ct);
        if (afectadas != 1) throw new InvalidOperationException("No se puede restaurar la liquidación del pago.");
        contexto.CambiosDeLiquidacion.Add(new CambioDeLiquidacion { LiquidacionId = orden.LiquidacionId,
            OrdenDePagoId = orden.Id, Operacion = OperacionDeLiquidacion.ReversionPagoPorOPP,
            UsuarioId = usuarioId, OcurridoEn = ahoraUtc });
        await contexto.SaveChangesAsync(ct);
    }
}
