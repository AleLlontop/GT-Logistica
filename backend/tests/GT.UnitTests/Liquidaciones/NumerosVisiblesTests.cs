using GT.Domain.Liquidaciones;

namespace GT.UnitTests.Liquidaciones;

/// <summary>El único lugar donde se arma el formato visible, sin ceros a la izquierda (research §4).</summary>
public class NumerosVisiblesTests
{
    [Theory]
    [InlineData(1, "LQ-1")]
    [InlineData(12, "LQ-12")]
    public void Liquidacion_LlevaPrefijoLQ(int numero, string esperado) =>
        Assert.Equal(esperado, NumerosVisibles.Liquidacion(numero));

    [Fact]
    public void OrdenDePago_LlevaPrefijoOP() => Assert.Equal("OP-3", NumerosVisibles.OrdenDePago(3));
}
