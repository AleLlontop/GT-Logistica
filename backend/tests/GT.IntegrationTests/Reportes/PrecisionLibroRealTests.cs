using ClosedXML.Excel;
using GT.Application.Reportes;
using GT.IntegrationTests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class PrecisionLibroRealTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private static ReporteTabular Reporte(IReadOnlyList<IReadOnlyList<CeldaDeReporte>> filas, IReadOnlyList<TotalDeReporte> totales) =>
        new(new("Precisión monetaria", "Prueba sintética", filas.Count, DateTime.UnixEpoch, "gerencia"),
            [ColumnaDeReporte.Texto("Documento"), ColumnaDeReporte.Importe("Importe"), ColumnaDeReporte.Importe("Otro importe")], filas, totales);

    [Fact]
    public void LibroGuardadoYReabiertoConservaCentavos_OrdinariosNegativosYGrandeRepresentable()
    {
        using var scope = app.Services.CreateScope();
        var modelo = Reporte([[CeldaDeReporte.Texto("F1"), CeldaDeReporte.Importe(1.10m), CeldaDeReporte.Importe(-0.01m)],
            [CeldaDeReporte.Texto("F2"), CeldaDeReporte.Importe(1000000000000000m), CeldaDeReporte.Importe(null)]],
            [new("Total representable", 1000000000000000m)]);
        var bytes = scope.ServiceProvider.GetRequiredService<IArmadorReporteExcel>().Armar(modelo);
        using var memoria = new MemoryStream(bytes); using var libro = new XLWorkbook(memoria);
        var hoja = libro.Worksheets.First();
        Assert.Equal(XLDataType.Number, hoja.Cell(7, 2).DataType);
        Assert.Equal(1.10m, hoja.Cell(7, 2).GetValue<decimal>());
        Assert.Equal(-0.01m, hoja.Cell(7, 3).GetValue<decimal>());
        Assert.Equal(1000000000000000m, hoja.Cell(8, 2).GetValue<decimal>());
        Assert.True(hoja.Cell(8, 3).IsEmpty());
        Assert.Equal(1000000000000000m, hoja.Cell(10, 3).GetValue<decimal>());
    }

    [Theory]
    [InlineData("celdaIntermedia")]
    [InlineData("soloTotal")]
    [InlineData("sumaMayorQueFila")]
    public void PerdidaEnUnaCoordenadaDescartaTodo_ExcepcionTipadaYPdfDisponible(string caso)
    {
        using var scope = app.Services.CreateScope();
        var perdido = caso == "sumaMayorQueFila" ? 10000000000000000.01m : 9999999999999999.99m;
        var modelo = Reporte([[CeldaDeReporte.Texto("Antes"), CeldaDeReporte.Importe(1.10m), CeldaDeReporte.Importe(0m)],
            [CeldaDeReporte.Texto("Intermedia"), CeldaDeReporte.Importe(caso == "celdaIntermedia" ? perdido : 2.01m), CeldaDeReporte.Importe(0.01m)],
            [CeldaDeReporte.Texto("Después"), CeldaDeReporte.Importe(3.02m), CeldaDeReporte.Importe(0m)]],
            [new("Total", caso == "celdaIntermedia" ? 1.10m : perdido)]);
        byte[]? entregado = null;
        var error = Assert.Throws<PrecisionMonetariaXlsxException>(() => entregado = scope.ServiceProvider.GetRequiredService<IArmadorReporteExcel>().Armar(modelo));
        Assert.Null(entregado);
        Assert.Equal("No podemos exportar este reporte a Excel sin perder centavos. Consultalo en pantalla o generá el PDF.", error.Message);
        var pdf = scope.ServiceProvider.GetRequiredService<IArmadorReportePdf>().Armar(modelo);
        Assert.Equal("%PDF"u8.ToArray(), pdf[..4]);
        Assert.Equal(perdido, caso == "celdaIntermedia" ? modelo.Filas[1][1].ValorImporte : modelo.Totales[0].Importe);
    }
}
