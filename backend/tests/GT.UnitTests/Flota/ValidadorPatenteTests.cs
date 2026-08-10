using GT.Domain.Flota;

namespace GT.UnitTests.Flota;

/// <summary>FR-004: los dos formatos argentinos que conviven hoy, sobre la patente ya normalizada.</summary>
public class ValidadorPatenteTests
{
    [Theory]
    [InlineData("ABC123")]   // Formato viejo
    [InlineData("AAA111")]
    [InlineData("AB123CD")]  // Mercosur
    [InlineData("ZZ999ZZ")]
    public void AceptaLosDosFormatosVigentes(string patente)
    {
        Assert.True(ValidadorPatente.EsValida(patente));
    }

    [Theory]
    [InlineData("")]           // Vacía
    [InlineData("AB12CD")]     // Largo distinto
    [InlineData("ABC1234")]    // Un dígito de más
    [InlineData("AB123C")]     // Le falta una letra final
    [InlineData("123ABC")]     // Dígitos y letras en el orden equivocado
    [InlineData("A1B2C3")]     // Alternados
    [InlineData("ABCD12")]     // Cuatro letras
    public void RechazaLoQueNoCumpleNingunFormato(string patente)
    {
        Assert.False(ValidadorPatente.EsValida(patente));
    }

    /// <summary>
    /// El validador trabaja sobre el valor <b>ya normalizado</b>. Pasarle uno sin normalizar lo
    /// rechaza, y por eso el orden en el caso de uso es normalizar primero (research §6).
    /// </summary>
    [Fact]
    public void SinNormalizarPrimero_RechazaUnaPatenteQueEsValida()
    {
        Assert.False(ValidadorPatente.EsValida("AB-123-CD"));
        Assert.True(ValidadorPatente.EsValida(NormalizadorPatente.Normalizar("AB-123-CD")));
    }

    [Fact]
    public void EnMinusculas_RechazaHastaQueSeNormaliza()
    {
        Assert.False(ValidadorPatente.EsValida("ab123cd"));
        Assert.True(ValidadorPatente.EsValida(NormalizadorPatente.Normalizar("ab123cd")));
    }
}
