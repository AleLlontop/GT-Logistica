using GT.Application.Proveedores;

namespace GT.UnitTests.Proveedores;

public class ReglasProveedorTests
{
    [Fact]
    public void NormalizaSinCopiarNiExigirContactoOpcional()
    {
        var datos = ReglasProveedor.Normalizar(new("proveedorNormal", "  Acopio  ", "30-71234567-1"));
        Assert.Equal("30712345671", datos.Cuit);
        Assert.Equal("Acopio", datos.RazonSocial);
        Assert.Null(datos.Email);
        Assert.Null(datos.Telefono);
        Assert.Equal(datos, ReglasProveedor.Normalizar(datos)); // conservar CUIT propio
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("30712345670")]
    public void RechazaCuitInvalido(string? cuit) =>
        Assert.Equal("cuit", Assert.Throws<ProveedorException>(() =>
            ReglasProveedor.Normalizar(new("proveedorNormal", "Proveedor", cuit))).Campo);

    [Theory]
    [InlineData("sin-arroba")]
    [InlineData("a@")]
    [InlineData("a@dominio")]
    [InlineData("a b@dominio.com")]
    public void ValidaEmailSoloCuandoSeInforma(string email) =>
        Assert.Equal("email", Assert.Throws<ProveedorException>(() =>
            ReglasProveedor.Normalizar(new("proveedorNormal", "Proveedor", "30712345671", Email: email))).Campo);

    [Fact]
    public void CamposPorTipoYCapacidadesNoSeTruncan()
    {
        Assert.Throws<ProveedorException>(() => ReglasProveedor.Normalizar(new("fletero")));
        Assert.Throws<ProveedorException>(() => ReglasProveedor.Normalizar(new("fletero", Cuit: "20123456786", TransportistaId: 1)));
        Assert.Throws<ProveedorException>(() => ReglasProveedor.Normalizar(new("proveedorNormal", "Proveedor", "30712345671", TransportistaId: 1)));
        Assert.Throws<ProveedorException>(() => ReglasProveedor.Normalizar(new("proveedorNormal", new string('x', 201), "30712345671")));
        Assert.Throws<ProveedorException>(() => ReglasProveedor.Normalizar(new("proveedorNormal", "Proveedor", "30712345671", Telefono: new string('1', 31))));
        Assert.Throws<ProveedorException>(() => ReglasProveedor.Normalizar(new("proveedorNormal", "Proveedor", "30712345671", Direccion: new string('x', 201))));
        Assert.Throws<ProveedorException>(() => ReglasProveedor.Normalizar(new("1", "Proveedor", "30712345671")));
        var fletero = ReglasProveedor.Normalizar(new("fletero", TransportistaId: 1));
        Assert.Null(fletero.Cuit);
        Assert.Null(fletero.RazonSocial);
    }

    [Fact]
    public void EdicionNoTieneCampoDeEstadoYVersionEsObligatoria()
    {
        Assert.Null(typeof(ProveedorPeticion).GetProperty("Activo"));
        Assert.Throws<ProveedorException>(() => ReglasProveedor.Normalizar(new("fletero", TransportistaId: 1), edicion: true));
        Assert.Equal(3, ReglasProveedor.Normalizar(new("fletero", TransportistaId: 1, Version: 3), edicion: true).Version);
    }

    [Fact]
    public void GestionarSoloAnunciaElPadronYNoOtorgaPermisosDeFactura()
    {
        var opciones = GT.Application.Autenticacion.CatalogoOpcionesMenu.Autorizadas([GT.Domain.Usuarios.CodigosPermiso.ProveedoresGestionar]);
        Assert.Equal("/proveedores", Assert.Single(opciones).Ruta);
    }
}
