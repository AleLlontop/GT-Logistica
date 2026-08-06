using GT.Domain.Choferes;
using GT.Domain.Personas;
using GT.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public class GtDbContext(DbContextOptions<GtDbContext> opciones) : DbContext(opciones)
{
    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<Rol> Roles => Set<Rol>();

    public DbSet<Permiso> Permisos => Set<Permiso>();

    public DbSet<Persona> Personas => Set<Persona>();

    public DbSet<Transportista> Transportistas => Set<Transportista>();

    public DbSet<Chofer> Choferes => Set<Chofer>();

    public DbSet<DocumentacionTipo> DocumentacionTipos => Set<DocumentacionTipo>();

    public DbSet<Documentacion> Documentaciones => Set<Documentacion>();

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.ApplyConfigurationsFromAssembly(typeof(GtDbContext).Assembly);
    }
}
