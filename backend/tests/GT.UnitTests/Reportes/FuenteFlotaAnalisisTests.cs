using GT.Application.Reportes.Analisis;
using GT.Application.Reportes.Fuentes;

namespace GT.UnitTests.Reportes;

public class FuenteFlotaAnalisisTests
{
    [Theory]
    [InlineData("disponible", "Disponible")]
    [InlineData("fueraDeServicio", "Fuera de servicio")]
    public void PalabrasDeFlotaCoincidenConPantalla(string codigo, string palabra) =>
        Assert.Equal(palabra, FuenteAnalisisFlota.EstadoVisible(codigo));

    [Fact]
    public void CeroYBajaSeConservan_ConteosSonEnteros_SinVehiculoEstaSeparado()
    {
        var contexto = ContextoAnalisis.DesdeInstante(new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero),
            new(new(2026, 10, 1), new(2026, 10, 31)), CriterioTemporalAnalisis.FechaViaje);
        var fila = new FilaFlotaAnalisis(1, "ZZ001AA", new(2, "Tipo actual"), new(3, "Dueño actual"), false, "disponible", "fueraDeServicio", 0);
        var datos = new AnalisisFlota(contexto, new(1, 0, 4), [], new(new([], null, 0), new([], null, 0)), new([fila], 1, 1, 20));
        var reporte = FuenteAnalisisFlota.Crear(datos, "Fecha del viaje", "gerencia");
        var celdas = Assert.Single(reporte.Filas);
        Assert.Equal("Dado de baja", celdas[3].ValorTexto);
        Assert.Equal("Disponible", celdas[4].ValorTexto);
        Assert.Equal("Fuera de servicio", celdas[5].ValorTexto);
        Assert.Equal(0, celdas[6].ValorEntero);
        Assert.All(reporte.Totales, t => Assert.True(t.EsCantidad));
        Assert.Equal(4m, reporte.Totales.Single(t => t.Etiqueta.Contains("fuera del filtro")).Importe);
    }
}
