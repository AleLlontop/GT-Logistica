using GT.Application.Reportes.Analisis;
using GT.Application.Reportes.Fuentes;

namespace GT.UnitTests.Reportes;

public class FuentesLiquidacionesAnalisisTests
{
    [Theory]
    [InlineData("pendiente", "Pendiente")]
    [InlineData("pagada", "Pagada")]
    [InlineData("anulada", "Anulada")]
    public void PalabrasDeLiquidacionesCoincidenConPantalla(string codigo, string palabra) =>
        Assert.Equal(palabra, FuenteAnalisisLiquidaciones.EstadoVisible(codigo));

    [Fact]
    public void PagoRevertidoConservaImporteHistorico_OrigenYFecha_SinSumarloAlVigente()
    {
        var contexto = ContextoAnalisis.DesdeInstante(new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero),
            new(new(2026, 10, 1), new(2026, 10, 31)), CriterioTemporalAnalisis.FechaPago);
        var fila = new FilaPagoAnalisis(1, "OP-1", 2, "LQ-2", "2026-09", new(3, "Transportista"),
            new(2026, 10, 18), 121.01m, false, "opp", 4, "OPP-4", new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc), new(5, "anulador"));
        var datos = new AnalisisPagos(contexto, new(0, 0, 1, 121.01m), [], new(new([], null, 0)), new([fila], 1, 1, 20));
        var reporte = FuenteAnalisisPagos.Crear(datos, "Fecha de pago", "gerencia");
        var celdas = Assert.Single(reporte.Filas);
        Assert.Equal("OP-1", celdas[0].ValorTexto);
        Assert.Equal("2026-09", celdas[2].ValorTexto);
        Assert.Equal(121.01m, celdas[5].ValorImporte);
        Assert.Equal("OPP-4", celdas[7].ValorTexto);
        Assert.Equal("Revertida", celdas[8].ValorTexto);
        Assert.Equal("01/11/2026 09:00", celdas[9].ValorTexto);
        Assert.Equal("anulador", celdas[10].ValorTexto);
        Assert.Equal(0m, reporte.Totales.Single(t => t.Etiqueta == "Pagado vigente en el período").Importe);
        Assert.Equal(121.01m, reporte.Totales.Single(t => t.Etiqueta.StartsWith("Importe revertido")).Importe);
    }
}
