namespace GT.Application.Caja;

/// <summary>
/// Detalle de una caja con sus totales y su saldo actual, calculados al leer (convención [003]). <b>Es
/// también la relectura de la apertura y del cierre</b> (convención [006]).
/// </summary>
public class ConsultarDetalleCaja(IRepositorioCaja cajas)
{
    /// <param name="usuarioId">El usuario en sesión: decide <c>PuedeOperar</c> (FR-035).</param>
    public Task<CajaDetalle?> EjecutarAsync(int id, int usuarioId, CancellationToken cancelacion = default) =>
        cajas.ObtenerDetalleAsync(id, usuarioId, cancelacion);

    /// <summary>La caja abierta del usuario en sesión, o <c>null</c>: lo que decide *Abrir caja* en pantalla.</summary>
    public Task<CajaDetalle?> AbiertaDeAsync(int usuarioId, CancellationToken cancelacion = default) =>
        cajas.ObtenerCajaAbiertaDeAsync(usuarioId, cancelacion);
}
