using GT.Domain.Proveedores;
using GT.Api.Proveedores;
using System.Text.Json;
using System.Globalization;

namespace GT.UnitTests.Proveedores;

public class ReglasFacturaProveedorTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 16);

    [Theory]
    [InlineData("100000.00", "21000.00", "121000.00")]
    [InlineData("0.50", "0.11", "0.61")]
    [InlineData("0.01", "0.00", "0.01")]
    [InlineData("100000000000000.01", "21000000000000.00", "121000000000000.01")]
    [InlineData("100000000000000.02", "21000000000000.00", "121000000000000.02")]
    public void CalculaExactamenteSinExigirCaeVigenteHoy(string neto, string iva, string total)
    {
        var fecha = Hoy.AddYears(-3);
        var importes = ReglasFacturaProveedor.Validar(" 0001-00003 ", fecha, "Flete", decimal.Parse(neto, CultureInfo.InvariantCulture), "CAE antiguo", fecha, Hoy);
        Assert.Equal(decimal.Parse(iva, CultureInfo.InvariantCulture), importes.Iva);
        Assert.Equal(decimal.Parse(total, CultureInfo.InvariantCulture), importes.Total);
    }

    [Theory]
    [InlineData(null)] [InlineData("0")] [InlineData("-1")] [InlineData("0.001")]
    [InlineData("9999999999999999.99")]
    public void NetoObligatorioPositivoDosDecimalesYResultadosRepresentables(string? neto)
    {
        Assert.Equal("neto", Assert.Throws<ReglaFacturaProveedorException>(() => ReglasFacturaProveedor.Validar("1", Hoy, "Detalle",
            neto is null ? null : decimal.Parse(neto, CultureInfo.InvariantCulture), "CAE", Hoy, Hoy)).Campo);
    }

    [Fact]
    public void ValidaTextosYBordesDeFechasSinLeerReloj()
    {
        Assert.Throws<ReglaFacturaProveedorException>(() => ReglasFacturaProveedor.Validar(" ", Hoy, "Detalle", 1m, "CAE", Hoy, Hoy));
        Assert.Throws<ReglaFacturaProveedorException>(() => ReglasFacturaProveedor.Validar(new string('1',401), Hoy, "Detalle", 1m, "CAE", Hoy, Hoy));
        Assert.Throws<ReglaFacturaProveedorException>(() => ReglasFacturaProveedor.Validar("1", Hoy, "", 1m, "CAE", Hoy, Hoy));
        Assert.Throws<ReglaFacturaProveedorException>(() => ReglasFacturaProveedor.Validar("1", Hoy, "Detalle", 1m, "", Hoy, Hoy));
        Assert.Throws<ReglaFacturaProveedorException>(() => ReglasFacturaProveedor.Validar("1", Hoy.AddDays(1), "Detalle", 1m, "CAE", Hoy.AddDays(1), Hoy));
        Assert.Throws<ReglaFacturaProveedorException>(() => ReglasFacturaProveedor.Validar("1", Hoy, "Detalle", 1m, "CAE", Hoy.AddDays(-1), Hoy));
    }

    [Theory]
    [InlineData("100000000000000.01")] [InlineData("100000000000000.02")]
    public void ConverterLocalConservaCentavosYRechazaJsonNumber(string importe)
    {
        var opciones = new JsonSerializerOptions(); opciones.Converters.Add(new ImporteDecimalJsonConverter());
        var numero = decimal.Parse(importe, CultureInfo.InvariantCulture);
        Assert.Equal('"' + importe + '"', JsonSerializer.Serialize(numero, opciones));
        Assert.Equal(numero, JsonSerializer.Deserialize<decimal>('"' + importe + '"', opciones));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<decimal>(importe, opciones));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<decimal>("\"1.001\"", opciones));
    }
}
