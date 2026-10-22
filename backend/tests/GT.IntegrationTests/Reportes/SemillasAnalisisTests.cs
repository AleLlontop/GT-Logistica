using GT.Domain.Usuarios;
using GT.Infrastructure.DatosIniciales;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class SemillasAnalisisTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task NuevosPermisosSonExplicitosEIdempotentesSinCambiarEmision()
    {
        string[] nuevos = [CodigosPermiso.ReportesConsultar, CodigosPermiso.ReportesFlotaConsultar];
        await app.EnLaBaseAsync(async db =>
        {
            Assert.Equal(2, await db.Permisos.CountAsync(p => nuevos.Contains(p.Codigo)));
            var roles = await db.Roles.Include(r => r.Permisos).ToListAsync();
            foreach (var rol in roles)
            {
                var autorizado = rol.Codigo is CodigosRol.Gerencia or CodigosRol.AdministradorSistema;
                Assert.Equal(autorizado ? 2 : 0, rol.Permisos.Count(p => nuevos.Contains(p.Codigo)));
                Assert.Equal(autorizado, rol.Permisos.Any(p => p.Codigo == CodigosPermiso.ReportesEmitir));
            }
        });
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SembradorInicial>().SembrarAsync(null);
        await app.EnLaBaseAsync(async db => Assert.Equal(2, await db.Permisos.CountAsync(p => nuevos.Contains(p.Codigo))));
    }
}
