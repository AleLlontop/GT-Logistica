using GT.Domain.Choferes;

namespace GT.UnitTests.Choferes;

/// <summary>
/// Cubre FR-025: el DNI, el CUIL y el CUIT se normalizan a sólo dígitos antes de validar su
/// unicidad, tanto al crear como al modificar.
/// </summary>
public class NormalizadorDocumentoNumericoTests
{
    [Theory]
    [InlineData("20-12345678-3")]
    [InlineData("20123456783")]
    [InlineData("20.12345678.3")]
    [InlineData("  20 12345678 3  ")]
    [InlineData("20/12345678/3")]
    public void Normalizar_ResuelveAlMismoNumero_SinImportarComoSeEscriba(string escrito)
    {
        // Es el caso límite de la spec: "20-12345678-3" y "20123456783" no pueden convivir como dos
        // registros distintos.
        Assert.Equal("20123456783", NormalizadorDocumentoNumerico.Normalizar(escrito));
    }

    [Theory]
    [InlineData("38.123.456", "38123456")]
    [InlineData("38123456", "38123456")]
    [InlineData(" 38 123 456 ", "38123456")]
    public void Normalizar_TambienSirveParaElDni(string escrito, string esperado)
    {
        Assert.Equal(esperado, NormalizadorDocumentoNumerico.Normalizar(escrito));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sin dígitos")]
    public void Normalizar_DevuelveVacio_CuandoNoQuedaNingunDigito(string? escrito)
    {
        Assert.Equal(string.Empty, NormalizadorDocumentoNumerico.Normalizar(escrito));
    }

    [Fact]
    public void Normalizar_DejaIntactoLoQueYaEraSoloDigitos()
    {
        Assert.Equal("27301234568", NormalizadorDocumentoNumerico.Normalizar("27301234568"));
    }

    [Fact]
    public void Normalizar_DescartaCualquierLetraIntercalada()
    {
        Assert.Equal("20123456783", NormalizadorDocumentoNumerico.Normalizar("CUIT 20-12345678-3"));
    }
}
