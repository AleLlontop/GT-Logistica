using GT.Domain.Choferes;

namespace GT.Application.Pagos;

public class CambiarEstadoChequePago(IRepositorioPagos pagos, TimeProvider reloj)
{
    public Task<DetalleCheque?> EjecutarAsync(int id, EstadoChequeEntrada entrada, int usuarioId, CancellationToken ct = default)
    {
        var ahora = reloj.GetUtcNow();
        return pagos.CambiarEstadoAsync(id, entrada, usuarioId, FechaHoyArgentina.Desde(ahora), ahora.UtcDateTime, ct);
    }
}
