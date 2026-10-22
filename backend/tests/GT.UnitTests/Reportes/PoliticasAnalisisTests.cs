using GT.Api.Autorizacion;
using GT.Api.Reportes;
using GT.Domain.Usuarios;
using Microsoft.AspNetCore.Authorization;

namespace GT.UnitTests.Reportes;

public class PoliticasAnalisisTests
{
    [Theory]
    [InlineData(CodigosPermiso.ViajesConsultar)]
    [InlineData(CodigosPermiso.FacturacionConsultar)]
    [InlineData(CodigosPermiso.LiquidacionesConsultar)]
    [InlineData(CodigosPermiso.CajaConsultar)]
    [InlineData(CodigosPermiso.ReportesFlotaConsultar)]
    [InlineData(CodigosPermiso.ChoferesVencimientosConsultar)]
    [InlineData(CodigosPermiso.FlotaVencimientosConsultar)]
    public void LecturaYExportacionExigenTodosSusPermisos(string fuente)
    {
        var opciones = new AuthorizationOptions();
        opciones.AgregarPoliticasAnalisisReportes();
        var lectura = opciones.GetPolicy(PoliticasAutorizacion.ParaTodos(CodigosPermiso.ReportesConsultar, fuente));
        var emision = opciones.GetPolicy(PoliticasAutorizacion.ParaTodos(CodigosPermiso.ReportesConsultar, fuente, CodigosPermiso.ReportesEmitir));
        Assert.NotNull(lectura);
        Assert.NotNull(emision);
        Assert.Equal(2, lectura.Requirements.Count);
        Assert.Equal(3, emision.Requirements.Count);
        Assert.All(emision.Requirements, r => Assert.IsType<PermisoRequirement>(r));
    }
}
