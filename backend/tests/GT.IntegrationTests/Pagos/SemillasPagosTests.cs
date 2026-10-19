using GT.Domain.Usuarios;
using GT.Infrastructure.DatosIniciales;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

public class SemillasPagosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task SietePermisosSoloAsignadosALosDosRolesAprobados()
    {
        await app.EnLaBaseAsync(async db =>
        {
            var permisos = await db.Permisos.Where(p => p.Codigo.StartsWith("pagos.")).Select(p => p.Codigo).ToListAsync();
            Assert.Equal(7, permisos.Count);
            var roles = await db.Roles.Include(r => r.Permisos).ToListAsync();
            foreach (var rol in roles)
            {
                var cantidad = rol.Permisos.Count(p => p.Codigo.StartsWith("pagos."));
                Assert.Equal(rol.Codigo is CodigosRol.Administracion or CodigosRol.AdministradorSistema ? 7 : 0, cantidad);
            }
        });
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<SembradorInicial>().SembrarAsync(null);
        await app.EnLaBaseAsync(async db => Assert.Equal(7, await db.Permisos.CountAsync(p => p.Codigo.StartsWith("pagos."))));
    }
}
