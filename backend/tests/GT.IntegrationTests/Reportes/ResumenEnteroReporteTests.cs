using ClosedXML.Excel;
using GT.Application.Reportes;
using GT.IntegrationTests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class ResumenEnteroReporteTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public void CantidadYDineroConservanTiposYFormatosDistintosEnLibroReal()
    {
        using var scope = app.Services.CreateScope();
        var reporte = new ReporteTabular(new("Resumen", "Período septiembre", 1,
            new DateTime(2026, 10, 1, 0, 30, 0, DateTimeKind.Utc), "gerencia"),
            [ColumnaDeReporte.Texto("Estado"), ColumnaDeReporte.Entero("Cantidad")],
            [[CeldaDeReporte.Texto("Realizados"), CeldaDeReporte.Entero(21)]],
            [TotalDeReporte.Cantidad("Viajes realizados", 21), new("Importe", 123.45m)]);
        using var bytes = new MemoryStream(scope.ServiceProvider.GetRequiredService<IArmadorReporteExcel>().Armar(reporte));
        using var libro = new XLWorkbook(bytes);
        var hoja = libro.Worksheets.First();
        Assert.Equal(XLDataType.Number, hoja.Cell(9, 2).DataType);
        Assert.Equal(21m, hoja.Cell(9, 2).GetValue<decimal>());
        Assert.Equal("#,##0", hoja.Cell(9, 2).Style.NumberFormat.Format);
        Assert.Equal(123.45m, hoja.Cell(10, 2).GetValue<decimal>());
        Assert.Equal("#,##0.00", hoja.Cell(10, 2).Style.NumberFormat.Format);
        var pdf = scope.ServiceProvider.GetRequiredService<IArmadorReportePdf>().Armar(reporte);
        Assert.Equal("%PDF"u8.ToArray(), pdf[..4]);
    }
}
