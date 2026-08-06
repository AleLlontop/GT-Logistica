using GT.Domain.Choferes;

namespace GT.UnitTests.Choferes;

/// <summary>
/// Cubre FR-017 y sus dos bordes declarados, más FR-017a (el día se corta en hora de Argentina) y
/// FR-019 (el documento cambia de estado solo con el paso de los días).
///
/// Todos los casos fijan el "hoy" en vez de leer el reloj: si dependieran de la fecha en que alguien
/// los corre, dejarían de probar el borde.
/// </summary>
public class CalculadorEstadoDocumentoTests
{
    private static readonly DateOnly Hoy = new(2026, 8, 6);

    [Fact]
    public void Vigente_CuandoElVencimientoEstaMasLejosQueLaVentanaDeAviso()
    {
        var vence = Hoy.AddDays(90);

        Assert.Equal(
            DocumentacionEstado.Vigente,
            CalculadorEstadoDocumento.Calcular(vence, diasAvisoVencimiento: 30, Hoy));
    }

    [Fact]
    public void ProximaAvencer_CuandoElVencimientoCaeDentroDeLaVentana()
    {
        var vence = Hoy.AddDays(10);

        Assert.Equal(
            DocumentacionEstado.ProximaAvencer,
            CalculadorEstadoDocumento.Calcular(vence, diasAvisoVencimiento: 30, Hoy));
    }

    [Fact]
    public void ProximaAvencer_EnElUltimoDiaDeLaVentana()
    {
        var vence = Hoy.AddDays(30);

        Assert.Equal(
            DocumentacionEstado.ProximaAvencer,
            CalculadorEstadoDocumento.Calcular(vence, diasAvisoVencimiento: 30, Hoy));
    }

    [Fact]
    public void Vencida_CuandoLaFechaYaPaso()
    {
        var vencio = Hoy.AddDays(-5);

        Assert.Equal(
            DocumentacionEstado.Vencida,
            CalculadorEstadoDocumento.Calcular(vencio, diasAvisoVencimiento: 30, Hoy));
    }

    // ── Borde 1: vence exactamente hoy ──────────────────────────────────────────────────────────

    [Fact]
    public void VenceExactamenteHoy_EsProximaAvencer_NoVencida()
    {
        Assert.Equal(
            DocumentacionEstado.ProximaAvencer,
            CalculadorEstadoDocumento.Calcular(Hoy, diasAvisoVencimiento: 30, Hoy));
    }

    [Fact]
    public void VencioAyer_RecienAhiEsVencida()
    {
        Assert.Equal(
            DocumentacionEstado.Vencida,
            CalculadorEstadoDocumento.Calcular(Hoy.AddDays(-1), diasAvisoVencimiento: 30, Hoy));
    }

    // ── Borde 2: tipo con cero días de aviso ────────────────────────────────────────────────────

    [Fact]
    public void ConCeroDiasDeAviso_NoHayPeriodoIntermedio()
    {
        // Vence dentro de 5 días y el tipo no avisa: sigue vigente, sin pasar por próxima a vencer.
        Assert.Equal(
            DocumentacionEstado.Vigente,
            CalculadorEstadoDocumento.Calcular(Hoy.AddDays(5), diasAvisoVencimiento: 0, Hoy));
    }

    [Fact]
    public void ConCeroDiasDeAviso_ElDiaDelVencimientoEsProximaAvencer()
    {
        Assert.Equal(
            DocumentacionEstado.ProximaAvencer,
            CalculadorEstadoDocumento.Calcular(Hoy, diasAvisoVencimiento: 0, Hoy));
    }

    // ── FR-019: cambia solo con el paso de los días ─────────────────────────────────────────────

    [Fact]
    public void ElMismoDocumento_CambiaDeEstadoSoloConElPasoDeLosDias()
    {
        var vence = new DateOnly(2026, 9, 5);
        const int diasAviso = 30;

        // Sin que nadie toque el documento, el mismo dato da tres estados distintos según el día.
        Assert.Equal(
            DocumentacionEstado.Vigente,
            CalculadorEstadoDocumento.Calcular(vence, diasAviso, new DateOnly(2026, 7, 1)));

        Assert.Equal(
            DocumentacionEstado.ProximaAvencer,
            CalculadorEstadoDocumento.Calcular(vence, diasAviso, new DateOnly(2026, 8, 20)));

        Assert.Equal(
            DocumentacionEstado.Vencida,
            CalculadorEstadoDocumento.Calcular(vence, diasAviso, new DateOnly(2026, 9, 6)));
    }

    // ── FR-017a: el día se corta en hora de Argentina ───────────────────────────────────────────

    [Fact]
    public void ElDiaSeCortaEnHoraDeArgentina_NoEnUtc()
    {
        // 6 de agosto 02:00 UTC son todavía las 23:00 del 5 en Argentina.
        var instante = new DateTimeOffset(2026, 8, 6, 2, 0, 0, TimeSpan.Zero);

        Assert.Equal(new DateOnly(2026, 8, 5), FechaHoyArgentina.Desde(instante));
    }

    [Fact]
    public void UnDocumentoQueVenceHoy_NoSeAdelantaAVencidoPorElHusoDelServidor()
    {
        // Mismo instante que el caso anterior: en UTC ya es día 6, en Argentina sigue siendo el 5.
        // Un documento que vence el 5 tiene que seguir siendo próxima a vencer, no vencida.
        var instante = new DateTimeOffset(2026, 8, 6, 2, 0, 0, TimeSpan.Zero);
        var hoyEnArgentina = FechaHoyArgentina.Desde(instante);

        Assert.Equal(
            DocumentacionEstado.ProximaAvencer,
            CalculadorEstadoDocumento.Calcular(new DateOnly(2026, 8, 5), 30, hoyEnArgentina));
    }

    // ── Días hasta el vencimiento (lo que muestra el panel) ─────────────────────────────────────

    [Theory]
    [InlineData(10, 10)]
    [InlineData(0, 0)]
    [InlineData(-5, -5)]
    public void DiasHastaVencimiento_EsNegativoCuandoYaVencio(int desplazamiento, int esperado)
    {
        var vence = Hoy.AddDays(desplazamiento);

        Assert.Equal(esperado, CalculadorEstadoDocumento.DiasHastaVencimiento(vence, Hoy));
    }
}
