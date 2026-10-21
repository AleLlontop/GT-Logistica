using System.Globalization;
using System.Text.Json;
using GT.Api.Reportes;
using GT.Api.Proveedores;
using GT.Application.Reportes.Analisis;

namespace GT.UnitTests.Reportes;

public class SerializacionAnalisisTests
{
    [Theory]
    [InlineData("0.00")] [InlineData("0.01")] [InlineData("-121.33")]
    [InlineData("9999999999999999.99")] [InlineData("19999999999999999.98")]
    [InlineData("79228162514264337593543950335.00")]
    public void DineroExactoConSignoYAgregadosRoundTrip(string importe)
    {
        var valor = decimal.Parse(importe, CultureInfo.InvariantCulture);
        var json = JsonSerializer.Serialize(valor, SerializadorAnalisisReportes.Opciones);
        Assert.Equal('"' + importe + '"', json);
        Assert.Equal(valor, JsonSerializer.Deserialize<decimal>(json, SerializadorAnalisisReportes.Opciones));
    }

    [Theory]
    [InlineData("1.23")] [InlineData("\"1.234\"")] [InlineData("\"01.00\"")]
    [InlineData("\"-0.00\"")] [InlineData("\"1e2\"")] [InlineData("\"1,10\"")]
    [InlineData("\"79228162514264337593543950336.00\"")]
    public void FormatoInvalidoOLecturaConPerdidaSeRechaza(string json) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<decimal>(json, SerializadorAnalisisReportes.Opciones));

    [Fact]
    public void EscribirNoRedondeaYNoAlteraContrato013()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize(1.234m, SerializadorAnalisisReportes.Opciones));
        Assert.False(ImporteDecimalJsonConverter.IntentarLeer("-1.00", out _));
        Assert.False(ImporteDecimalJsonConverter.IntentarLeer("19999999999999999.98", out _));
        Assert.Equal("121.33", JsonSerializer.Serialize(121.33m));
    }

    [Fact]
    public void FechaInstanteYEnumUsanContratoLocal()
    {
        var contexto = ContextoAnalisis.DesdeInstante(DateTimeOffset.Parse("2026-10-18T22:00:00-03:00"),
            new(new(2026, 9, 15), new(2026, 10, 10)), CriterioTemporalAnalisis.FechaCobro);
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(contexto, SerializadorAnalisisReportes.Opciones));
        Assert.Equal("2026-10-19T01:00:00Z", json.RootElement.GetProperty("consultadoEn").GetString());
        Assert.Equal("2026-10-18", json.RootElement.GetProperty("hoyArgentina").GetString());
        Assert.Equal("fechaCobro", json.RootElement.GetProperty("criterioTemporal").GetString());
        Assert.Equal(contexto, JsonSerializer.Deserialize<ContextoAnalisis>(json.RootElement.GetRawText(), SerializadorAnalisisReportes.Opciones));
    }
}
