using GT.Domain.Facturacion;

namespace GT.UnitTests.Facturacion;

/// <summary>El formato <c>0000-00000000</c> del número de comprobante (FR-027).</summary>
public class NumeroDeComprobanteTests
{
    [Theory]
    [InlineData("0014-00000003")]
    [InlineData("0001-00000001")]
    [InlineData("9999-99999999")]
    [InlineData("0000-00000000")]
    public void ElFormatoValidoSeAcepta(string valor) =>
        Assert.True(NumeroDeComprobante.EsValido(valor));

    [Theory]
    [InlineData("14-3")]                // sin ceros a la izquierda
    [InlineData("0014-3")]              // correlativo corto
    [InlineData("014-00000003")]        // punto de venta corto
    [InlineData("00014-00000003")]      // punto de venta largo
    [InlineData("0014-000000003")]      // correlativo largo
    [InlineData("0014 00000003")]       // separado por espacio
    [InlineData("0014/00000003")]       // otro separador
    [InlineData("001A-00000003")]       // con una letra
    [InlineData("0014-00000003 ")]      // con espacio al final
    [InlineData("")]
    [InlineData(null)]
    public void ElFormatoInvalidoSeRechaza(string? valor) =>
        Assert.False(NumeroDeComprobante.EsValido(valor));

    /// <summary>
    /// El largo declarado tiene que coincidir con el de la columna: <c>nvarchar(13)</c>. Si alguien
    /// cambiara el formato sin cambiar la columna, el número se truncaría al guardarlo.
    /// </summary>
    [Fact]
    public void ElLargoDeclaradoCoincideConElDelFormato()
    {
        Assert.Equal(NumeroDeComprobante.Largo, "0014-00000003".Length);
        Assert.True(NumeroDeComprobante.EsValido(new string('0', 4) + "-" + new string('0', 8)));
    }

    /// <summary>
    /// El número que la pantalla <b>propone</b>, no el que asigna: quien emite lo puede cambiar, porque
    /// el correlativo real sale de AFIP/ARCA por fuera del sistema (FR-027).
    /// </summary>
    [Theory]
    [InlineData("0014", 3, "0014-00000003")]
    [InlineData("14", 3, "0014-00000003")]
    [InlineData("0001", 12345678, "0001-12345678")]
    public void ArmaElNumeroQueLaPantallaPropone(string puntoDeVenta, int correlativo, string esperado)
    {
        var armado = NumeroDeComprobante.Armar(puntoDeVenta, correlativo);

        Assert.Equal(esperado, armado);
        Assert.True(NumeroDeComprobante.EsValido(armado));
    }
}
