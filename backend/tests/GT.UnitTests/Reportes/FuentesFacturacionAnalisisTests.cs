using GT.Application.Reportes.Analisis;
using GT.Application.Reportes.Fuentes;

namespace GT.UnitTests.Reportes;

public class FuentesFacturacionAnalisisTests
{
    [Fact]
    public void ColumnasMonetariasPersistidas_YFechaCobroNula_NoSeRecalculan()
    {
        var contexto = ContextoAnalisis.DesdeInstante(new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero),
            new(new(2026, 10, 1), new(2026, 10, 31)), CriterioTemporalAnalisis.FechaFactura);
        var fila = new FilaFacturaAnalisis(1, "0015-00000001", new(2026, 10, 1), 3, "Snapshot", "pendiente", 100.01m, 21m, 121.01m, null);
        var datos = new AnalisisFacturacion(contexto, new(1, 121.01m, 0, 121.01m, 0), [],
            new(new([], null, 0), new([], null, 0)), new([fila], 1, 1, 20));
        var reporte = FuenteAnalisisFacturacion.Crear(datos, "Fecha de factura", "gerencia");
        var celdas = Assert.Single(reporte.Filas);
        Assert.Equal("Snapshot", celdas[2].ValorTexto);
        Assert.Equal("Pendiente", celdas[3].ValorTexto);
        Assert.Equal(100.01m, celdas[4].ValorImporte);
        Assert.Equal(21m, celdas[5].ValorImporte);
        Assert.Equal(121.01m, celdas[6].ValorImporte);
        Assert.True(celdas[7].EstaVacia);
        Assert.Equal(121.01m, reporte.Totales.Single(t => t.Etiqueta == "Pendiente vigente de estos documentos").Importe);
    }
}
