using GT.Domain.Caja;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

/// <summary>Lock y escritura caller-owned compartidos con 011; jamás Begin/Commit.</summary>
public class PrimitivasMovimientosCaja(GtDbContext contexto)
{
    private void ExigirTransaccion()
    {
        if (contexto.Database.CurrentTransaction is null)
            throw new InvalidOperationException("La operación de Caja requiere una transacción del llamador.");
    }
    public Task<int> TomarAsync(int cajaId, int? responsableId, CancellationToken ct = default)
    {
        ExigirTransaccion();
        var cajas = contexto.Cajas.Where(x => x.Id == cajaId && x.Estado == EstadoCaja.Abierta);
        if (responsableId.HasValue) cajas = cajas.Where(x => x.UsuarioResponsableId == responsableId.Value);
        return cajas.ExecuteUpdateAsync(u => u.SetProperty(x => x.Estado, x => x.Estado), ct);
    }
    /// <summary>Sólo leer después del lock; todos los importes conservan decimal.</summary>
    public async Task<decimal> SaldoAsync(int cajaId, CancellationToken ct = default)
    {
        ExigirTransaccion();
        var inicial = await contexto.Cajas.Where(x => x.Id == cajaId).Select(x => x.SaldoInicial).SingleAsync(ct);
        var movimientos = await contexto.MovimientosDeCaja.Where(x => x.CajaId == cajaId)
            .SumAsync(x => (decimal?)(x.Tipo == TipoMovimientoCaja.Ingreso ? x.Importe : -x.Importe), ct) ?? 0m;
        return inicial + movimientos;
    }
    /// <summary>El caller ya tomó la caja; no impone saldo positivo a movimientos manuales de 011.</summary>
    public async Task InsertarAsync(MovimientoDeCaja movimiento, CancellationToken ct = default)
    {
        ExigirTransaccion();
        contexto.MovimientosDeCaja.Add(movimiento);
        await contexto.SaveChangesAsync(ct);
    }
}
