using GT.Domain.Flota;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class TipoVehiculoConfiguracion : IEntityTypeConfiguration<TipoVehiculo>
{
    public void Configure(EntityTypeBuilder<TipoVehiculo> tabla)
    {
        tabla.ToTable("TiposVehiculo");
        tabla.HasKey(tipo => tipo.Id);

        tabla.Property(tipo => tipo.Nombre).HasMaxLength(100).IsRequired();
        tabla.Property(tipo => tipo.Activo).IsRequired();

        // Cierra la carrera entre dos altas simultáneas del mismo nombre, que ninguna consulta previa
        // evita (FR-009, convención [003]).
        tabla.HasIndex(tipo => tipo.Nombre).IsUnique();
    }
}
