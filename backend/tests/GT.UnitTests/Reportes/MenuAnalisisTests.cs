using GT.Application.Autenticacion;
using GT.Domain.Usuarios;
namespace GT.UnitTests.Reportes;
public class MenuAnalisisTests
{
    [Fact]
    public void BaseAbreSoloReportes()
    {
        var opcion = Assert.Single(CatalogoOpcionesMenu.Autorizadas([CodigosPermiso.ReportesConsultar]));
        Assert.Equal("reportes", opcion.Codigo); Assert.Equal("/reportes", opcion.Ruta);
    }
    [Theory]
    [InlineData(CodigosPermiso.ReportesFlotaConsultar)]
    [InlineData(CodigosPermiso.ReportesEmitir)]
    public void FuenteOEmisionNoAbrenReportes(string permiso)
    {
        Assert.DoesNotContain(CatalogoOpcionesMenu.Autorizadas([permiso]), x => x.Codigo == "reportes");
    }
}
