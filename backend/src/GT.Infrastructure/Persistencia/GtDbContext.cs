using GT.Domain.Adelantos;
using GT.Domain.Choferes;
using GT.Domain.Facturacion;
using GT.Domain.Flota;
using GT.Domain.Liquidaciones;
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

    // ── Módulo 6: gestión de facturación ───────────────────────────────────────────────────────
    /// <summary>
    /// Configuración única de todo el sistema: **una sola fila**, garantizada por un <c>CHECK</c> en
    /// la base y no por la disciplina del código. La fila no existe hasta el primer guardado
    /// (Módulo 6, research §12).
    /// </summary>
    public DbSet<EmpresaEmisora> EmpresaEmisora => Set<EmpresaEmisora>();

    /// <summary>
    /// La tabla se llama <c>Facturas</c>, que es como la nombra el negocio; la entidad
    /// <c>FacturaCliente</c>, para dejar lugar a la liquidación al transportista, que también es una
    /// factura y todavía no existe.
    /// </summary>
    public DbSet<FacturaCliente> Facturas => Set<FacturaCliente>();

    /// <summary>
    /// Historial de FR-045 **y** registro de correcciones de FR-037, en la misma tabla. No se escribe
    /// desde ningún endpoint: la alimentan los casos de uso, en la misma transacción que el cambio.
    /// </summary>
    public DbSet<CambioDeEstadoFactura> CambiosDeEstadoFactura => Set<CambioDeEstadoFactura>();

    // ── Módulo 9: liquidación a transportistas ─────────────────────────────────────────────────
    public DbSet<Liquidacion> Liquidaciones => Set<Liquidacion>();

    /// <summary>
    /// Qué viajes agrupa cada liquidación, y qué agrupaba cada anulada. Tabla propia y no una columna en
    /// <c>Viajes</c>: el Módulo 5 no se toca y la anulada no pierde sus viajes (Módulo 9, research §1).
    /// </summary>
    public DbSet<LiquidacionViaje> LiquidacionViajes => Set<LiquidacionViaje>();

    public DbSet<OrdenDePago> OrdenesDePago => Set<OrdenDePago>();

    /// <summary>Historial de FR-033. Lo alimentan los casos de uso, en la misma transacción que el cambio.</summary>
    public DbSet<CambioDeLiquidacion> CambiosDeLiquidacion => Set<CambioDeLiquidacion>();

    public DbSet<CambioDeLiquidacionViaje> CambiosDeLiquidacionViajes => Set<CambioDeLiquidacionViaje>();

    // ── Módulo 10: gestión de adelantos de sueldo ──────────────────────────────────────────────
    /// <summary>
    /// No se borran ni se modifican: sólo cambian de estado, con un <c>UPDATE</c> condicional (Módulo 10,
    /// research §2). Lee <c>Personas</c>, <c>Choferes</c> y <c>Transportistas</c> sin agregarles nada.
    /// </summary>
    public DbSet<Adelanto> Adelantos => Set<Adelanto>();

    /// <summary>Historial de FR-036. Lo alimentan los casos de uso, en la misma transacción que el cambio.</summary>
    public DbSet<CambioDeAdelanto> CambiosDeAdelanto => Set<CambioDeAdelanto>();

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

        // Módulo 9: el mismo mecanismo y la misma trampa del número de viaje. La migración les aplica el
        // mismo `NO CACHE`: son números que se ven y se nombran en los mensajes (research §4).
        modelo.HasSequence<int>(LiquidacionConfiguracion.Secuencia).StartsAt(1).IncrementsBy(1);
        modelo.HasSequence<int>(OrdenDePagoConfiguracion.Secuencia).StartsAt(1).IncrementsBy(1);
    }
}
