using GT.Domain.Viajes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class CambioDeEstadoViajeConfiguracion : IEntityTypeConfiguration<CambioDeEstadoViaje>
{
    public void Configure(EntityTypeBuilder<CambioDeEstadoViaje> tabla)
    {
        tabla.ToTable("CambiosDeEstadoViaje");
        tabla.HasKey(cambio => cambio.Id);

        tabla.Property(cambio => cambio.ViajeId).IsRequired();

        // Anulable **sólo** para el registro del alta: antes del alta no había estado (FR-035).
        tabla.Property(cambio => cambio.EstadoAnterior).HasConversion<byte?>();

        tabla.Property(cambio => cambio.EstadoNuevo).HasConversion<byte>().IsRequired();
        tabla.Property(cambio => cambio.UsuarioId).IsRequired();
        tabla.Property(cambio => cambio.OcurridoEn).IsRequired();

        // Sirve para dos cosas: mostrar el historial ordenado en la ficha, y resolver la subconsulta
        // correlacionada que deriva `demorado` (FR-039, research §6).
        tabla.HasIndex(cambio => new { cambio.ViajeId, cambio.OcurridoEn });

        // **Única cascada del módulo.** El historial no tiene vida propia: si algún día un viaje se
        // borrara, sus líneas no tendrían a quién pertenecer. No hay endpoint que borre viajes, así
        // que hoy no se ejercita.
        tabla.HasOne(cambio => cambio.Viaje)
            .WithMany(viaje => viaje.CambiosDeEstado)
            .HasForeignKey(cambio => cambio.ViajeId)
            .OnDelete(DeleteBehavior.Cascade);

        // El usuario, en cambio, en Restrict: el Módulo 2 usa baja lógica y el historial tiene que
        // seguir diciendo quién hizo cada cambio aunque esa cuenta ya no opere (FR-035).
        tabla.HasOne(cambio => cambio.Usuario)
            .WithMany()
            .HasForeignKey(cambio => cambio.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
