using ClosedXML.Excel;
using GT.Application.Reportes;
using GT.Infrastructure.Reportes;

namespace GT.UnitTests.Reportes;

public class ManifestMonetarioXlsxTests
{
    [Fact]
    public void IncluyeTodasLasColumnasYTotalesMonetarios_SinNulosNiCantidades()
    {
        var reporte = new ReporteTabular(new("Importes", "Octubre", 2, DateTime.UnixEpoch, "gerencia"),
            [ColumnaDeReporte.Texto("Número"), ColumnaDeReporte.Importe("Neto"), ColumnaDeReporte.Importe("IVA"), ColumnaDeReporte.Importe("Total")],
            [[CeldaDeReporte.Texto("F1"), CeldaDeReporte.Importe(100.01m), CeldaDeReporte.Importe(21m), CeldaDeReporte.Importe(121.01m)],
             [CeldaDeReporte.Texto("F2"), CeldaDeReporte.Importe(null), CeldaDeReporte.Importe(-0.01m), CeldaDeReporte.Importe(0m)]],
            [new("Total", 121m), TotalDeReporte.Cantidad("Cantidad", 2)]);
        var preparado = ArmadorReporteExcelClosedXml.Preparar(reporte);
        using var libro = preparado.Libro;
        Assert.Equal(6, preparado.Manifest.Count);
        Assert.Equal(new[] { 100.01m, 21m, 121.01m, -0.01m, 0m, 121m }, preparado.Manifest.Select(c => c.Fuente));
        Assert.Equal(6, preparado.Manifest.Select(c => (c.Hoja, c.Fila, c.Columna)).Distinct().Count());
        Assert.All(preparado.Manifest, c =>
        {
            var celda = libro.Worksheet(c.Hoja).Cell(c.Fila, c.Columna);
            Assert.Equal(XLDataType.Number, celda.DataType);
            Assert.Equal("#,##0.00", celda.Style.NumberFormat.Format);
            Assert.True(VerificadorMonetarioXlsx.Coincide(c.Fuente, celda.GetDouble()));
        });
        Assert.DoesNotContain(preparado.Manifest, c => c.Fila == 8 && c.Columna == 2);
        Assert.DoesNotContain(preparado.Manifest, c => c.Fila == 11); // Resumen entero, no monetario.
    }
}
