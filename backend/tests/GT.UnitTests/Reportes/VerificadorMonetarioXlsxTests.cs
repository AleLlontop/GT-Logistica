using System.Globalization;
using GT.Infrastructure.Reportes;

namespace GT.UnitTests.Reportes;

public class VerificadorMonetarioXlsxTests
{
    [Theory]
    [InlineData("0")]
    [InlineData("0.01")]
    [InlineData("1.10")]
    [InlineData("1234567.89")]
    [InlineData("-1234567.89")]
    [InlineData("1000000000000000.00")]
    [InlineData("10000000000000000.00")]
    [InlineData("-10000000000000000.00")]
    [InlineData("35184372088832.01")]
    public void AceptaIgualdadMonetaria_SinExigirIdentidadBinaria(string texto)
    {
        var fuente = decimal.Parse(texto, CultureInfo.InvariantCulture);
        Assert.True(VerificadorMonetarioXlsx.Coincide(fuente, decimal.ToDouble(fuente)));
    }

    [Theory]
    [InlineData("9999999999999999.99")]
    [InlineData("-9999999999999999.99")]
    [InlineData("10000000000000000.01")]
    [InlineData("140737488355328.01")]
    [InlineData("70368744177664.01")]
    public void RechazaCentavoPerdido_EnFronteraMaximoOSumaMayorQueFila(string texto)
    {
        var fuente = decimal.Parse(texto, CultureInfo.InvariantCulture);
        Assert.False(VerificadorMonetarioXlsx.Coincide(fuente, decimal.ToDouble(fuente)));
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(double.MaxValue)]
    public void LecturaNoMonetariaNoPasa(double recuperado) => Assert.False(VerificadorMonetarioXlsx.Coincide(0m, recuperado));

    [Fact]
    public void NoRedondeaFuenteNiAceptaDiferenciaDeCentavo()
    {
        Assert.False(VerificadorMonetarioXlsx.Coincide(1.001m, 1d));
        Assert.False(VerificadorMonetarioXlsx.Coincide(1.10m, 1.11d));
        Assert.True(VerificadorMonetarioXlsx.Coincide(0.10m, 0.1d));
    }
}
