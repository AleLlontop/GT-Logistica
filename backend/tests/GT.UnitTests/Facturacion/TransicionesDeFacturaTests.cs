using GT.Domain.Facturacion;

namespace GT.UnitTests.Facturacion;

/// <summary>
/// Las transiciones de la factura (FR-043).
///
/// <b>La mitad del valor de este archivo es la ausencia de caminos</b>: lo que verifica no es sólo que las
/// dos transiciones válidas se permitan, sino que <b>ninguna otra exista</b> — en particular ningún camino
/// de retroceso.
/// </summary>
public class TransicionesDeFacturaTests
{
    [Theory]
    [InlineData(EstadoFactura.Pendiente, EstadoFactura.Pagada)]
    [InlineData(EstadoFactura.Pendiente, EstadoFactura.Anulada)]
    [InlineData(EstadoFactura.Pagada, EstadoFactura.Pendiente)]
    public void Las_TransicionesValidas_SePermiten(EstadoFactura actual, EstadoFactura pedido) =>
        Assert.True(TransicionesDeFactura.EstaPermitida(actual, pedido));

    /// <summary>
    /// No se devuelve una anulada a <c>pendiente</c> ni pasa a <c>pagada</c>, y una pagada no pasa a <c>anulada</c>.
    /// </summary>
    [Theory]
    [InlineData(EstadoFactura.Anulada, EstadoFactura.Pendiente)]
    [InlineData(EstadoFactura.Pagada, EstadoFactura.Anulada)]
    [InlineData(EstadoFactura.Anulada, EstadoFactura.Pagada)]
    public void TransicionesInvalidas_NoSePermiten(EstadoFactura actual, EstadoFactura pedido) =>
        Assert.False(TransicionesDeFactura.EstaPermitida(actual, pedido));

    /// <summary>Nada se transiciona a sí mismo: cobrar una pagada no es una operación válida.</summary>
    [Theory]
    [InlineData(EstadoFactura.Pendiente)]
    [InlineData(EstadoFactura.Pagada)]
    [InlineData(EstadoFactura.Anulada)]
    public void Ningun_EstadoSeTransicionaASiMismo(EstadoFactura estado) =>
        Assert.False(TransicionesDeFactura.EstaPermitida(estado, estado));

    /// <summary>
    /// El mapa completo, enumerado: de las nueve combinaciones posibles, <b>exactamente tres</b> se
    /// permiten (incluyendo revertir cobro).
    /// </summary>
    [Fact]
    public void De_LasNueveCombinaciones_SoloTresSePermiten()
    {
        var estados = Enum.GetValues<EstadoFactura>();

        var permitidas = estados
            .SelectMany(actual => estados.Select(pedido => (actual, pedido)))
            .Where(par => TransicionesDeFactura.EstaPermitida(par.actual, par.pedido))
            .ToList();

        Assert.Equal(3, permitidas.Count);
        Assert.Contains((EstadoFactura.Pendiente, EstadoFactura.Pagada), permitidas);
        Assert.Contains((EstadoFactura.Pendiente, EstadoFactura.Anulada), permitidas);
        Assert.Contains((EstadoFactura.Pagada, EstadoFactura.Pendiente), permitidas);
    }

    [Theory]
    [InlineData(EstadoFactura.Pagada, true)]
    [InlineData(EstadoFactura.Anulada, true)]
    [InlineData(EstadoFactura.Pendiente, false)]
    public void Los_DosTerminalesSonPagadaYAnulada(EstadoFactura estado, bool esperado) =>
        Assert.Equal(esperado, TransicionesDeFactura.EsTerminal(estado));

    /// <summary>
    /// <b>Terminal no significa inmutable del todo</b>, y la diferencia importa: una factura <c>pagada</c>
    /// no admite más cambios de estado <b>pero sí se corrige</b> —corregir un CAE mal tipeado no le toca
    /// el estado ni la fecha de cobro (FR-035, US4 esc. 8)—. La única inmutable de verdad es la
    /// <c>anulada</c> (FR-038).
    ///
    /// Este test existe para que nadie use <c>EsTerminal</c> como si significara "no se puede tocar".
    /// </summary>
    [Fact]
    public void EsTerminal_HablaDeTransicionesYNoDeInmutabilidad()
    {
        Assert.True(TransicionesDeFactura.EsTerminal(EstadoFactura.Pagada));

        // Y sin embargo se corrige: la regla de qué se puede corregir vive en `CorregirFactura`, que
        // rechaza sólo la anulada.
        Assert.True(TransicionesDeFactura.EsTerminal(EstadoFactura.Anulada));
    }
}
