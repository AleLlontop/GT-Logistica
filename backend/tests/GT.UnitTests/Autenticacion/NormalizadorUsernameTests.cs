using GT.Domain.Autenticacion;

namespace GT.UnitTests.Autenticacion;

/// <summary>Cubre FR-012: el username se normaliza igual al validar que al crear la cuenta.</summary>
public class NormalizadorUsernameTests
{
    [Theory]
    [InlineData("admin")]
    [InlineData("ADMIN")]
    [InlineData("Admin")]
    [InlineData("  admin  ")]
    [InlineData("  ADMIN")]
    [InlineData("aDmIn   ")]
    public void Normalizar_ResuelveALaMismaCuenta_SinImportarMayusculasNiEspacios(string escrito)
    {
        Assert.Equal("ADMIN", NormalizadorUsername.Normalizar(escrito));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Normalizar_DevuelveVacio_CuandoNoHayNadaEscrito(string? escrito)
    {
        Assert.Equal(string.Empty, NormalizadorUsername.Normalizar(escrito));
    }

    [Fact]
    public void Normalizar_NoRecortaEspaciosInternos()
    {
        // Sólo se recortan los extremos: un espacio en el medio hace a un username distinto.
        Assert.Equal("JUAN PEREZ", NormalizadorUsername.Normalizar("  juan perez "));
    }
}
