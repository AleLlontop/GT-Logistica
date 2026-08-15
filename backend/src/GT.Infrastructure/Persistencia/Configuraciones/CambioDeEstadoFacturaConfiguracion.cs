using GT.Domain.Facturacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class CambioDeEstadoFacturaConfiguracion : IEntityTypeConfiguration<CambioDeEstadoFactura>
{
    public void Configure(EntityTypeBuilder<CambioDeEstadoFactura> tabla)
    {
        tabla.ToTable("CambiosDeEstadoFactura");
        tabla.HasKey(cambio => cambio.Id);

        tabla.Property(cambio => cambio.FacturaId).IsRequired();

        // Anulable en la emisión —antes no había estado— y en una corrección, que no cambia ninguno.
        tabla.Property(cambio => cambio.EstadoAnterior).HasConversion<byte?>();

        // **Anulable, a diferencia del historial del Módulo 5.** Ahí `EstadoNuevo` era obligatorio
        // porque toda línea era un cambio de estado; acá la misma tabla registra también las
        // correcciones de FR-037, y la ausencia de estado nuevo **es** la marca de que lo son. Una
        // columna `EsCorreccion` repetiría un dato que ya está y podría discrepar de él.
        tabla.Property(cambio => cambio.EstadoNuevo).HasConversion<byte?>();

        tabla.Property(cambio => cambio.UsuarioId).IsRequired();
        tabla.Property(cambio => cambio.OcurridoEn).IsRequired();

        // Derivada de `EstadoNuevo`: no es columna.
        tabla.Ignore(cambio => cambio.EsCorreccion);

        // Para mostrar el historial ordenado en la ficha (FR-045).
        tabla.HasIndex(cambio => new { cambio.FacturaId, cambio.OcurridoEn });

        // Cascada sólo a efectos del modelo: nada borra facturas y no hay endpoint que lo haga.
        tabla.HasOne(cambio => cambio.Factura)
            .WithMany(factura => factura.CambiosDeEstado)
            .HasForeignKey(cambio => cambio.FacturaId)
            .OnDelete(DeleteBehavior.Cascade);

        // El usuario en Restrict: el Módulo 2 usa baja lógica y el historial tiene que seguir diciendo
        // quién hizo cada cambio aunque esa cuenta ya no opere (FR-045).
        tabla.HasOne(cambio => cambio.Usuario)
            .WithMany()
            .HasForeignKey(cambio => cambio.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
