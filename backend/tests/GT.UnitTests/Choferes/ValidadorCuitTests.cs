using GT.Domain.Choferes;

namespace GT.UnitTests.Choferes;

/// <summary>
/// Cubre FR-003 y FR-007: el CUIT y el CUIL se validan con el dígito verificador, no sólo por
/// longitud. Un número tipeado de más pasaría el control de largo y se descubriría recién cuando
/// alguien intenta facturar.
/// </summary>
public class ValidadorCuitTests
{
    [Theory]
    [InlineData("20123456786")]
    [InlineData("30712345671")]
    [InlineData("27301234568")]
    public void EsValido_CuandoElVerificadorCierra(string cuit)
    {
        Assert.True(ValidadorCuit.EsValido(cuit));
    }

    [Theory]
    [InlineData("20-12345678-6")]
    [InlineData("20.12345678.6")]
    [InlineData("  20 12345678 6  ")]
    public void EsValido_AceptaGuionesPuntosYEspacios_PorqueNormalizaAntes(string escrito)
    {
        // FR-025: se normaliza antes de validar, así el mismo CUIT escrito de varias formas es el
        // mismo número.
        Assert.True(ValidadorCuit.EsValido(escrito));
    }

    [Theory]
    [InlineData("20123456780")]
    [InlineData("20123456781")]
    [InlineData("30712345670")]
    public void NoEsValido_CuandoElVerificadorNoCierra(string cuit)
    {
        Assert.False(ValidadorCuit.EsValido(cuit));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("2012345678")]
    [InlineData("201234567861")]
    [InlineData("abcdefghijk")]
    public void NoEsValido_CuandoNoSonOnceDigitos(string? valor)
    {
        Assert.False(ValidadorCuit.EsValido(valor));
    }

    // ── Las dos excepciones del algoritmo ───────────────────────────────────────────────────────

    [Fact]
    public void ConRestoCero_ElVerificadorEsCero()
    {
        Assert.True(ValidadorCuit.EsValido("20000000060"));
    }

    [Fact]
    public void ConRestoUno_ElVerificadorEsNueve()
    {
        Assert.True(ValidadorCuit.EsValido("20000000019"));
    }

    [Fact]
    public void ParaCadaPrefijo_HayExactamenteUnVerificadorValido()
    {
        // Es la propiedad que sostiene todo el algoritmo: si hubiera dos, no serviría para detectar
        // errores de tipeo; si no hubiera ninguno, habría prefijos imposibles de registrar.
        const string prefijo = "2012345678";

        var validos = Enumerable.Range(0, 10)
            .Count(verificador => ValidadorCuit.EsValido(prefijo + verificador));

        Assert.Equal(1, validos);
    }
}
