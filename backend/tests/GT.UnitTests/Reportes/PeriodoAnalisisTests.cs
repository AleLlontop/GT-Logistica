using GT.Application.Reportes.Analisis;

namespace GT.UnitTests.Reportes;

public class PeriodoAnalisisTests
{
    [Fact]
    public void RangoInvertidoYFechasAusentesSeRechazan()
    {
        Assert.Throws<ArgumentException>(() => new PeriodoAnalisis(new(2026, 10, 2), new(2026, 10, 1)));
        Assert.Throws<ArgumentException>(() => PeriodoAnalisis.Requerido(null, new(2026, 10, 1)));
        Assert.Throws<ArgumentException>(() => PeriodoAnalisis.Requerido(new(2026, 10, 1), null));
    }

    [Fact]
    public void DiasExactosSonInclusivos_PeroMesesLiquidadosSonCompletos()
    {
        var p = new PeriodoAnalisis(new(2026, 9, 15), new(2026, 10, 10));
        Assert.True(p.Incluye(new(2026, 9, 15)));
        Assert.True(p.Incluye(new(2026, 10, 10)));
        Assert.False(p.Incluye(new(2026, 9, 14)));
        Assert.False(p.Incluye(new(2026, 10, 11)));
        Assert.True(p.IncluyeMes(2026, 9));
        Assert.True(p.IncluyeMes(2026, 10));
        Assert.False(p.IncluyeMes(2026, 8));
        Assert.False(p.IncluyeMes(2026, 11));
    }

    [Theory]
    [InlineData("2024-03-01T01:00:00Z", "2024-02-01", "2024-02-29")]
    [InlineData("2026-01-01T01:00:00Z", "2025-12-01", "2025-12-31")]
    [InlineData("2026-10-19T01:00:00Z", "2026-10-01", "2026-10-31")]
    public void MesPropuestoUsaArgentinaYAniosBisiestos(string instante, string desde, string hasta)
    {
        var p = PeriodoAnalisis.MesActual(DateTimeOffset.Parse(instante));
        Assert.Equal(DateOnly.Parse(desde), p.Desde);
        Assert.Equal(DateOnly.Parse(hasta), p.Hasta);
    }

    [Fact]
    public void CajaCortaDiasArgentinosConHastaExclusivo()
    {
        var rango = new PeriodoAnalisis(new(2026, 10, 18), new(2026, 10, 18)).ParaInstantesArgentina();
        Assert.Equal(new DateTime(2026, 10, 18, 3, 0, 0, DateTimeKind.Utc), rango.DesdeInclusivo);
        Assert.Equal(new DateTime(2026, 10, 19, 3, 0, 0, DateTimeKind.Utc), rango.HastaExclusivo);
    }

    [Fact]
    public void ExtremosDeDateOnlyNoDesbordanNiRecortanElDia()
    {
        var p = new PeriodoAnalisis(DateOnly.MinValue, DateOnly.MaxValue);
        Assert.True(p.IncluyeMes(9999, 12));
        Assert.Throws<ArgumentOutOfRangeException>(() => p.ParaInstantesArgentina());
        Assert.Equal(DateTimeKind.Utc, new PeriodoAnalisis(DateOnly.MinValue, DateOnly.MinValue).ParaInstantesArgentina().DesdeInclusivo.Kind);
    }

    [Fact]
    public void ContextoCapturaRelojUnaVezYComparteHoyYMeses()
    {
        var reloj = new RelojContado();
        var contexto = ContextoAnalisis.Capturar(reloj, new(new(2026, 9, 15), new(2026, 10, 10)), CriterioTemporalAnalisis.PeriodoLiquidado);
        Assert.Equal(1, reloj.Lecturas);
        Assert.Equal(new DateOnly(2026, 10, 18), contexto.HoyArgentina);
        Assert.Equal(TimeSpan.Zero, contexto.ConsultadoEn.Offset);
        Assert.Equal("2026-09", contexto.PeriodoLiquidadoDesde);
        Assert.Equal("2026-10", contexto.PeriodoLiquidadoHasta);
    }

    [Fact]
    public void SituacionActualNoSePresentaComoCorteHistorico()
    {
        var instante = DateTimeOffset.Parse("2026-10-19T01:00:00Z");
        var contexto = ContextoAnalisis.DesdeInstante(instante, null, CriterioTemporalAnalisis.SituacionActual);
        Assert.Null(contexto.Desde);
        Assert.Null(contexto.Hasta);
        Assert.Throws<ArgumentException>(() => ContextoAnalisis.DesdeInstante(instante, new(new(2026, 1, 1), new(2026, 1, 2)), CriterioTemporalAnalisis.SituacionActual));
        Assert.Throws<ArgumentException>(() => ContextoAnalisis.DesdeInstante(instante, null, CriterioTemporalAnalisis.FechaCobro));
    }

    private sealed class RelojContado : TimeProvider
    {
        public int Lecturas { get; private set; }
        public override DateTimeOffset GetUtcNow()
        {
            Lecturas++;
            return DateTimeOffset.Parse("2026-10-19T01:00:00Z");
        }
    }
}
