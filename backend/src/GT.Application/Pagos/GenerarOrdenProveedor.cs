using GT.Domain.Choferes;

namespace GT.Application.Pagos;

/// <summary>Captura una sola vez el reloj de la intención; la persistencia coordina su transacción.</summary>
public class GenerarOrdenProveedor(IRepositorioPagos pagos, TimeProvider reloj)
{
    public Task<ResultadoEscrituraPago> EjecutarAsync(GenerarOrdenEntrada entrada, int usuarioId,
        bool puedeConsultar, CancellationToken ct = default)
    {
        var ahora = reloj.GetUtcNow();
        return pagos.GenerarAsync(entrada, usuarioId, puedeConsultar, FechaHoyArgentina.Desde(ahora), ahora.UtcDateTime, ct);
    }
}
