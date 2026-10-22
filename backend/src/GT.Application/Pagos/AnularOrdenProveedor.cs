using GT.Domain.Choferes;
using GT.Domain.Pagos;

namespace GT.Application.Pagos;

public class AnularOrdenProveedor(IRepositorioPagos pagos, TimeProvider reloj)
{
    public Task<ResultadoEscrituraPago> EjecutarAsync(int id, AnularOrdenEntrada entrada, int usuarioId,
        bool puedeConsultar, CancellationToken ct = default)
    {
        ValidarEntrada(entrada);
        var ahora = reloj.GetUtcNow();
        return pagos.AnularAsync(id, entrada with { Motivo = entrada.Motivo!.Trim() }, usuarioId,
            puedeConsultar, FechaHoyArgentina.Desde(ahora), ahora.UtcDateTime, ct);
    }
    public static void ValidarEntrada(AnularOrdenEntrada entrada)
    {
        if (!entrada.Confirmado) throw new ReglaPagoException("confirmacion_requerida", "Confirmá explícitamente la anulación.", campo: "confirmado");
        if (entrada.ClaveOperacion == Guid.Empty) throw new ReglaPagoException("clave_invalida", "Informá la clave de operación.", campo: "claveOperacion");
        if (!ReglasDePagos.MotivoAnulacionValido(entrada.Motivo)) throw new ReglaPagoException("motivo_requerido", "Ingresá el motivo de anulación.", campo: "motivo");
    }
    /// <summary>El repositorio llama esta regla con datos releídos bajo todos los locks.</summary>
    public static void ValidarSituacion(EstadoOrdenPagoProveedor estado, IEnumerable<EstadoCheque> cheques,
        bool tieneTransferencia, bool cajaApta, bool pagosReversibles)
    {
        if (estado != EstadoOrdenPagoProveedor.Pendiente) throw new ReglaPagoException("orden_no_anulable", "La orden ya está Anulada.", 409);
        if (tieneTransferencia) throw new ReglaPagoException("orden_no_anulable", "Una orden con transferencias no admite anulación automática.", 409);
        if (cheques.Any(c => c != EstadoCheque.Pendiente)) throw new ReglaPagoException("orden_no_anulable", "Todos los cheques deben estar Pendientes para anular la orden.", 409);
        if (!cajaApta) throw new ReglaPagoException("caja_no_disponible", "La caja original debe continuar abierta y admitir la compensación.", 409);
        if (!pagosReversibles) throw new ReglaPagoException("pago_no_reversible", "No se pueden revertir los pagos propios de esta orden.", 409);
    }
}
