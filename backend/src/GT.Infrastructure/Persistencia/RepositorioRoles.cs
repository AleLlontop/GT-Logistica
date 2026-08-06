using GT.Application.Usuarios;
using GT.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public class RepositorioRoles(GtDbContext contexto) : IRepositorioRoles
{
    /// <summary>
    /// Los cuatro roles del sistema con sus permisos. El catálogo lo sembró el Módulo 1 y este
    /// módulo sólo lo lee (FR-010).
    /// </summary>
    public async Task<IReadOnlyList<Rol>> ObtenerTodosConPermisosAsync(
        CancellationToken cancelacion = default) =>
        await contexto.Roles
            .Include(rol => rol.Permisos)
            .AsNoTracking()
            .OrderBy(rol => rol.Nombre)
            .ToListAsync(cancelacion);
}
