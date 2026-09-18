namespace GT.Application.Caja;

/// <summary>
/// El resumen que se muestra antes de cerrar: saldo inicial, movimientos, totales y saldo final calculado
/// (FR-016, RN6). Sólo para quien puede cerrar: la caja tiene que existir, estar abierta y ser propia.
/// </summary>
public class ConsultarResumenDeCierre(IRepositorioCaja cajas)
{
    public async Task<ResultadoCaja> EjecutarAsync(int cajaId, int usuarioId, CancellationToken cancelacion = default)
    {
        if (await cajas.MotivoNoOperableAsync(cajaId, usuarioId, cancelacion) is { } motivo)
        {
            return ResultadoCaja.NoOperable(motivo);
        }

        return ResultadoCaja.Exito(await cajas.ConsultarResumenAsync(cajaId, cancelacion));
    }
}
