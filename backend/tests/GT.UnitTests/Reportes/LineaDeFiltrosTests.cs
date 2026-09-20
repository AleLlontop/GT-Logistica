using GT.Application.Reportes;

namespace GT.UnitTests.Reportes;

/// <summary>La línea de filtros del encabezado, con filtros y sin ninguno (FR-006, research §9).</summary>
public class LineaDeFiltrosTests
{
    [Fact]
    public void SinNingunFiltro_DiceQueNoHayNinguno() =>
        Assert.Equal("Sin filtros aplicados", new LineaDeFiltros().ToString());

    /// <summary>Un filtro no aplicado no figura: la línea dice lo que se aplicó, no lo que existe.</summary>
    [Fact]
    public void LosFiltrosVaciosNoFiguran() =>
        Assert.Equal(
            "Sin filtros aplicados",
            new LineaDeFiltros()
                .Con("Cliente", (string?)null)
                .Con("Búsqueda", "   ")
                .Con("Desde", (DateOnly?)null)
                .ToString());

    [Fact]
    public void ConUnSoloFiltro_NoLlevaSeparador() =>
        Assert.Equal(
            "Cliente: Aceitera del Sur",
            new LineaDeFiltros().Con("Cliente", "Aceitera del Sur").ToString());

    [Fact]
    public void ConVariosFiltros_VanUnidosPorElSeparador() =>
        Assert.Equal(
            "Cliente: Aceitera del Sur · Estado: En curso",
            new LineaDeFiltros()
                .Con("Cliente", "Aceitera del Sur")
                .Con("Estado", "En curso")
                .ToString());

    /// <summary>Las fechas en <c>dd/MM/yyyy</c>, como la pantalla las muestra.</summary>
    [Fact]
    public void LasFechasVanEnFormatoArgentino() =>
        Assert.Equal(
            "Desde: 01/09/2026 · Hasta: 20/09/2026",
            new LineaDeFiltros()
                .Con("Desde", new DateOnly(2026, 9, 1))
                .Con("Hasta", new DateOnly(2026, 9, 20))
                .ToString());
}
