using GT.Domain.Choferes;

namespace GT.IntegrationTests.Proveedores;

public class EscenarioProveedoresTests
{
    [Fact]
    public void IdentidadesDePruebaSonValidasYExternasAlEmisor()
    {
        Assert.True(ValidadorCuit.EsValido(EscenarioProveedores.Normal().Cuit));
        Assert.True(ValidadorCuit.EsValido(EscenarioProveedores.Emisor().Cuit));
        var externo = EscenarioProveedores.Externo();
        Assert.True(ValidadorCuit.EsValido(externo.Cuit));
        Assert.NotEqual(EscenarioProveedores.Emisor().Cuit, externo.Cuit);
        var fletero = EscenarioProveedores.Fletero(externo);
        Assert.Same(externo, fletero.Transportista);
        Assert.Null(fletero.Cuit);
        Assert.Null(fletero.RazonSocial);
        Assert.Null(fletero.Telefono);
        Assert.Null(fletero.Email);
        Assert.Same(externo, EscenarioProveedores.Pendiente(externo).Transportista);
    }
}
