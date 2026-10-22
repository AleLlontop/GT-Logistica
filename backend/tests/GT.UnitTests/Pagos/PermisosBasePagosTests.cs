using GT.Application.Autenticacion;
using GT.Domain.Usuarios;

namespace GT.UnitTests.Pagos;

public class PermisosBasePagosTests
{
    [Theory]
    [InlineData(CodigosPermiso.PagosGenerar, "/pagos/nueva")]
    [InlineData(CodigosPermiso.PagosConsultar, "/pagos/ordenes")]
    [InlineData(CodigosPermiso.PagosChequesConsultar, "/pagos/cheques")]
    [InlineData(CodigosPermiso.PagosBancosConsultar, "/pagos/bancos")]
    public void UnPermisoSoloDaSuEntrada(string permiso, string ruta)
    {
        var entrada = Assert.Single(CatalogoOpcionesMenu.Autorizadas([permiso]));
        Assert.Equal(ruta, entrada.Ruta);
    }

    [Theory]
    [InlineData(CodigosPermiso.PagosAnular)]
    [InlineData(CodigosPermiso.PagosChequesCambiarEstado)]
    [InlineData(CodigosPermiso.PagosBancosGestionar)]
    public void AccionSinPantallaPropia_NoConcedeMenu(string permiso) =>
        Assert.Empty(CatalogoOpcionesMenu.Autorizadas([permiso]));
}
