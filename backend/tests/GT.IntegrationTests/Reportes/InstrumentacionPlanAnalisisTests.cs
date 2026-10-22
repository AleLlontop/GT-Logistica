using System.Data;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Reportes;

public class InstrumentacionPlanAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task RecuperaPlanRealYMensajesIo_SinCambiarDatos()
    {
        using var scope=app.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<GtDbContext>();
        var nombre=db.Database.GetDbConnection().Database;
        var plan=await LecturasYPlanesAnalisis.MedirAsync(nombre,"sonda",new("SELECT COUNT(*) AS Cantidad FROM dbo.Viajes",[],IsolationLevel.Snapshot),"/tmp/gt015-medicion/sonda",CancellationToken.None);
        Assert.True(File.Exists(plan.Plan));Assert.NotEmpty(plan.Mensajes);
        Assert.Contains("ShowPlanXML",await File.ReadAllTextAsync(plan.Plan));
    }
}
