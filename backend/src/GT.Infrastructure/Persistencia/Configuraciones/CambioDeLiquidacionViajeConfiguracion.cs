using GT.Domain.Liquidaciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class CambioDeLiquidacionViajeConfiguracion : IEntityTypeConfiguration<CambioDeLiquidacionViaje>
{
    public void Configure(EntityTypeBuilder<CambioDeLiquidacionViaje> tabla)
    {
        tabla.ToTable("CambiosDeLiquidacionViajes");

        // La PK compuesta impide que una edición registre dos veces el mismo viaje.
        tabla.HasKey(cambio => new { cambio.CambioDeLiquidacionId, cambio.ViajeId });

        tabla.Property(cambio => cambio.Agregado).IsRequired();

        tabla.HasOne(cambio => cambio.Cambio)
            .WithMany(entrada => entrada.Viajes)
            .HasForeignKey(cambio => cambio.CambioDeLiquidacionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Sin navegación inversa desde `Viaje`: el Módulo 5 no se toca.
        tabla.HasOne(cambio => cambio.Viaje)
            .WithMany()
            .HasForeignKey(cambio => cambio.ViajeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
