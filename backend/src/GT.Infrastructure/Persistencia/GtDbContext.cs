using GT.Domain.Choferes;
using GT.Domain.Flota;
using GT.Domain.Personas;
using GT.Domain.Usuarios;
using GT.Domain.Viajes;
using GT.Infrastructure.Persistencia.Configuraciones;
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

    // ── Módulo 4: gestión de flota ─────────────────────────────────────────────────────────────
    public DbSet<TipoVehiculo> TiposVehiculo => Set<TipoVehiculo>();

    public DbSet<Vehiculo> Vehiculos => Set<Vehiculo>();

    /// <summary>
    /// Tabla propia, separada de <see cref="Documentaciones"/>: comparten la regla de vencimientos y
    /// el almacén de archivos, no las filas (Módulo 4, research §1).
    /// </summary>
    public DbSet<DocumentacionVehiculo> DocumentacionesVehiculo => Set<DocumentacionVehiculo>();

    // ── Módulo 5: gestión de viajes ────────────────────────────────────────────────────────────
    /// <summary>Padrón propio del módulo: el cliente existe para sostener al viaje (FR-053).</summary>
    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<Viaje> Viajes => Set<Viaje>();

    /// <summary>
    /// Historial de FR-035. No se escribe ni se modifica desde ningún endpoint: sólo lo alimentan los
    /// casos de uso que cambian el estado de un viaje, en la misma transacción que el cambio.
    /// </summary>
    public DbSet<CambioDeEstadoViaje> CambiosDeEstadoViaje => Set<CambioDeEstadoViaje>();

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

        // El número de viaje sale de una secuencia y no de la identidad de la tabla: una columna
        // IDENTITY de SQL Server **salta de a 1000 tras un apagado sucio**, y en un entorno que se
        // levanta y baja con `compose` eso pasa —el viaje siguiente al 12 sería el 1012, contra lo que
        // pide FR-011 y el escenario US2 esc. 5— (research §1).
        //
        // El `NO CACHE` que elimina ese salto no tiene API fluida: lo aplica la migración con un
        // ALTER, y va acompañado de un test que verifica que la numeración avanza de a uno.
        modelo.HasSequence<int>(ViajeConfiguracion.Secuencia).StartsAt(1).IncrementsBy(1);
    }
}
