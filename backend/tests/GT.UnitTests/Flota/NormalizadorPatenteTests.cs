using GT.Domain.Flota;

namespace GT.UnitTests.Flota;

/// <summary>
/// FR-003: la patente se normaliza antes de comparar. Es lo que hace que <c>ab 123 cd</c> y
/// <c>AB123CD</c> sean la misma unidad y no dos.
/// </summary>
public class NormalizadorPatenteTests
{
    [Theory]
    [InlineData("AB123CD")]
    [InlineData("ab123cd")]
    [InlineData("ab 123 cd")]
    [InlineData("AB-123-CD")]
    [InlineData("AB.123.CD")]
    [InlineData("  AB123CD  ")]
    public void TodasLasFormasDeEscribirla_DanElMismoValor(string escrita)
    {
        Assert.Equal("AB123CD", NormalizadorPatente.Normalizar(escrita));
    }

    [Fact]
    public void NormalizaTambienElFormatoViejo()
    {
        Assert.Equal("ABC123", NormalizadorPatente.Normalizar("abc-123"));
    }

    /// <summary>
    /// Las letras no se descartan, a diferencia de <c>NormalizadorDocumentoNumerico</c>: en una
    /// patente son la mitad del dato (research §6).
    /// </summary>
    [Fact]
    public void ConservaLasLetras()
    {
        Assert.Equal("AB123CD", NormalizadorPatente.Normalizar("AB123CD"));
    }

    [Fact]
    public void UnaCadenaVacia_QuedaVacia()
    {
        Assert.Equal(string.Empty, NormalizadorPatente.Normalizar(string.Empty));
    }

    [Fact]
    public void UnaCadenaDeSeparadores_QuedaVacia()
    {
        Assert.Equal(string.Empty, NormalizadorPatente.Normalizar(" - . "));
    }
}
