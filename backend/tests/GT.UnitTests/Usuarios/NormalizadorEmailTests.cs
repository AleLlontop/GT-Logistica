using GT.Domain.Usuarios;

namespace GT.UnitTests.Usuarios;

/// <summary>
/// Cubre FR-020: el email se normaliza igual al crear que al modificar, así variantes que sólo
/// difieren en mayúsculas o espacios no pueden convivir como usuarios distintos (FR-003).
/// </summary>
public class NormalizadorEmailTests
{
    [Theory]
    [InlineData("juan@gt.com")]
    [InlineData("JUAN@GT.COM")]
    [InlineData("Juan@Gt.Com")]
    [InlineData("  juan@gt.com  ")]
    [InlineData("  JUAN@GT.COM")]
    [InlineData("jUaN@gT.cOm   ")]
    public void Normalizar_ResuelveAlMismoEmail_SinImportarMayusculasNiEspacios(string escrito)
    {
        Assert.Equal("juan@gt.com", NormalizadorEmail.Normalizar(escrito));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalizar_DevuelveVacio_CuandoNoHayNadaEscrito(string? escrito)
    {
        Assert.Equal(string.Empty, NormalizadorEmail.Normalizar(escrito));
    }

    [Fact]
    public void Normalizar_DejaIntactoUnEmailQueYaEstabaNormalizado()
    {
        Assert.Equal("admin@gtlogistica.local", NormalizadorEmail.Normalizar("admin@gtlogistica.local"));
    }

    [Fact]
    public void Normalizar_NoTocaLosPuntosNiLosSignosDelNombre()
    {
        // El sistema no interpreta las convenciones de ningún proveedor: "juan.perez+gt@gt.com" y
        // "juanperez@gt.com" son dos direcciones distintas, y así se guardan.
        Assert.Equal("juan.perez+gt@gt.com", NormalizadorEmail.Normalizar(" Juan.Perez+GT@gt.com "));
    }
}
