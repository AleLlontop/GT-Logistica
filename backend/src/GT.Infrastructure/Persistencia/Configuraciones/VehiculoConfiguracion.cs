using GT.Domain.Flota;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class VehiculoConfiguracion : IEntityTypeConfiguration<Vehiculo>
{
    public void Configure(EntityTypeBuilder<Vehiculo> tabla)
    {
        tabla.ToTable("Vehiculos");
        tabla.HasKey(vehiculo => vehiculo.Id);

        tabla.Property(vehiculo => vehiculo.Patente).HasMaxLength(10).IsRequired();
        tabla.Property(vehiculo => vehiculo.Marca).HasMaxLength(50).IsRequired();
        tabla.Property(vehiculo => vehiculo.Modelo).HasMaxLength(50).IsRequired();
        tabla.Property(vehiculo => vehiculo.TipoVehiculoId).IsRequired();
        tabla.Property(vehiculo => vehiculo.TransportistaId).IsRequired();
        tabla.Property(vehiculo => vehiculo.Activo).IsRequired();

        tabla.Property(vehiculo => vehiculo.EstadoOperativo)
            .HasConversion<byte>()
            .IsRequired();

        // Único y **sin filtro por Activo**: la patente de una unidad dada de baja sigue ocupada, y
        // por eso registrarla de nuevo se rechaza pidiendo reactivar la existente (FR-002, FR-008f).
        tabla.HasIndex(vehiculo => vehiculo.Patente).IsUnique();

        // Para el filtro por transportista del listado (FR-030) y para contar los vehículos activos
        // de un transportista al intentar darlo de baja (FR-008d).
        tabla.HasIndex(vehiculo => vehiculo.TransportistaId);

        // Para el filtro por tipo (FR-030) y para contar los vehículos de un tipo al darlo de baja
        // (FR-010).
        tabla.HasIndex(vehiculo => vehiculo.TipoVehiculoId);

        // Nada se borra físicamente en este módulo salvo los documentos, así que el borrado en
        // cascada no tiene a quién servir.
        tabla.HasOne(vehiculo => vehiculo.Tipo)
            .WithMany(tipo => tipo.Vehiculos)
            .HasForeignKey(vehiculo => vehiculo.TipoVehiculoId)
            .OnDelete(DeleteBehavior.Restrict);

        tabla.HasOne(vehiculo => vehiculo.Transportista)
            .WithMany(transportista => transportista.Vehiculos)
            .HasForeignKey(vehiculo => vehiculo.TransportistaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
