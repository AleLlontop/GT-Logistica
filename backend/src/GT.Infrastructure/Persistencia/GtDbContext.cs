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

    /// <summary>
    /// Se aplica a las propiedades <c>DateTime</c> y <c>DateTime?</c> de todo el modelo. Los
    /// <c>DateOnly</c> —nacimiento, emisión, vencimiento— no entran: no son instantes y no tienen
    /// zona horaria que corregir.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configuracion)
    {
        configuracion.Properties<DateTime>().HaveConversion<ConversorInstanteUtc>();
    }

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.ApplyConfigurationsFromAssembly(typeof(GtDbContext).Assembly);
    }
}
