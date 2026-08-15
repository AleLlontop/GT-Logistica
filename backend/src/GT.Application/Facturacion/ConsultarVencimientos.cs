namespace GT.Application.Facturacion;

/// <summary>
/// El panel de vencimientos: las <c>vencida</c> y las que vencen dentro de los próximos <b>7 días
/// corridos</b> (FR-063).
///
/// Las <c>pagada</c> y <c>anulada</c> <b>no figuran</b>, y la exclusión va escrita como predicado de la
/// consulta y no como filtrado posterior: escrita así es una garantía y no algo que alguien pueda olvidar
/// (convención [004]).
///
/// Los días de atraso o de plazo se calculan <b>en la consulta</b>, contra el mismo día en curso que
/// acota la ventana. El día llega del <c>TimeProvider</c> registrado y no de <c>DateTime.UtcNow</c>, para
/// que un test pueda fijarlo en vez de esperar a que venza una factura (convención [005]).
/// </summary>
public class ConsultarVencimientos(IRepositorioFacturas facturas, TimeProvider reloj)
{
    /// <summary>Días corridos de la ventana. Fijo, no configurable: la spec lo decide (FR-063).</summary>
    public const int DiasDeLaVentana = 7;

    public Task<IReadOnlyList<FilaDeVencimiento>> EjecutarAsync(
        CancellationToken cancelacion = default) =>
        facturas.ConsultarVencimientosAsync(
            Domain.Choferes.FechaHoyArgentina.Desde(reloj.GetUtcNow()),
            cancelacion);
}
