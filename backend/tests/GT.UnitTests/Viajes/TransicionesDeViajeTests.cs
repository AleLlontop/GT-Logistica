using GT.Domain.Viajes;

namespace GT.UnitTests.Viajes;

/// <summary>
/// Cubre FR-033: las cuatro transiciones permitidas y las que la spec nombra explícitamente como
/// rechazadas, incluidas las que la pantalla nunca ofrece pero el endpoint igual tiene que rechazar.
/// </summary>
public class TransicionesDeViajeTests
{
    // ── Las cuatro permitidas ───────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(EstadoViaje.Pendiente, EstadoViaje.EnCurso)]
    [InlineData(EstadoViaje.EnCurso, EstadoViaje.Rendido)]
    [InlineData(EstadoViaje.Pendiente, EstadoViaje.Anulado)]
    [InlineData(EstadoViaje.EnCurso, EstadoViaje.Anulado)]
    public void LasCuatroPermitidas(EstadoViaje actual, EstadoViaje pedido) =>
        Assert.True(TransicionesDeViaje.EstaPermitida(actual, pedido));

    // ── El salto que la pantalla no ofrece (US4 esc. 10) ────────────────────────────────────────

    [Fact]
    public void PendienteARendido_SeRechaza()
    {
        // La pantalla no ofrece *Rendir* en un viaje pendiente, pero el endpoint lo verifica igual:
        // la regla no vive sólo en la pantalla.
        Assert.False(TransicionesDeViaje.EstaPermitida(EstadoViaje.Pendiente, EstadoViaje.Rendido));
    }

    // ── Los dos terminales no tienen salida ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(EstadoViaje.Pendiente)]
    [InlineData(EstadoViaje.EnCurso)]
    [InlineData(EstadoViaje.Rendido)]
    [InlineData(EstadoViaje.Anulado)]
    public void DesdeRendido_NoSaleNinguna(EstadoViaje pedido) =>
        Assert.False(TransicionesDeViaje.EstaPermitida(EstadoViaje.Rendido, pedido));

    [Theory]
    [InlineData(EstadoViaje.Pendiente)]
    [InlineData(EstadoViaje.EnCurso)]
    [InlineData(EstadoViaje.Rendido)]
    [InlineData(EstadoViaje.Anulado)]
    public void DesdeAnulado_NoSaleNinguna(EstadoViaje pedido) =>
        Assert.False(TransicionesDeViaje.EstaPermitida(EstadoViaje.Anulado, pedido));

    // ── Ningún estado se transiciona a sí mismo ─────────────────────────────────────────────────

    [Theory]
    [InlineData(EstadoViaje.Pendiente)]
    [InlineData(EstadoViaje.EnCurso)]
    [InlineData(EstadoViaje.Rendido)]
    [InlineData(EstadoViaje.Anulado)]
    public void NingunEstadoTransicionaASiMismo(EstadoViaje estado) =>
        Assert.False(TransicionesDeViaje.EstaPermitida(estado, estado));

    [Fact]
    public void EnCursoAPendiente_NoHayCaminoDeVuelta() =>
        Assert.False(TransicionesDeViaje.EstaPermitida(EstadoViaje.EnCurso, EstadoViaje.Pendiente));

    // ── Terminalidad, que es lo que consultan los cinco caminos de escritura (FR-018) ────────────

    [Theory]
    [InlineData(EstadoViaje.Rendido, true)]
    [InlineData(EstadoViaje.Anulado, true)]
    [InlineData(EstadoViaje.Pendiente, false)]
    [InlineData(EstadoViaje.EnCurso, false)]
    public void EsTerminal_DistingueLosDosEstadosCerrados(EstadoViaje estado, bool esperado) =>
        Assert.Equal(esperado, TransicionesDeViaje.EsTerminal(estado));
}
