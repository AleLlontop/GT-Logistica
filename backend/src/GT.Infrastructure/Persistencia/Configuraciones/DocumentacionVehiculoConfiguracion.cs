using GT.Domain.Flota;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class DocumentacionVehiculoConfiguracion : IEntityTypeConfiguration<DocumentacionVehiculo>
{
    public void Configure(EntityTypeBuilder<DocumentacionVehiculo> tabla)
    {
        tabla.ToTable("DocumentacionesVehiculo");
        tabla.HasKey(documento => documento.Id);

        tabla.Property(documento => documento.VehiculoId).IsRequired();
        tabla.Property(documento => documento.DocumentacionTipoId).IsRequired();

        // Sin índice único: una póliza conserva su número al renovarse, así que dos documentos del
        // mismo vehículo y tipo pueden repetirlo (FR-016).
        tabla.Property(documento => documento.Numero).HasMaxLength(50).IsRequired();

        tabla.Property(documento => documento.FechaEmision).IsRequired();
        tabla.Property(documento => documento.FechaVencimiento).IsRequired();

        tabla.Property(documento => documento.ArchivoRuta).HasMaxLength(400);
        tabla.Property(documento => documento.ArchivoNombre).HasMaxLength(255);
        tabla.Property(documento => documento.ArchivoTipoContenido).HasMaxLength(100);

        // Cubre la ficha del vehículo y, sobre todo, la elección del documento vigente de cada tipo:
        // el de vencimiento más lejano, sin ordenar en memoria (FR-024).
        tabla.HasIndex(documento => new
            {
                documento.VehiculoId,
                documento.DocumentacionTipoId,
                documento.FechaVencimiento,
            })
            .HasDatabaseName("IX_DocumentacionesVehiculo_VehiculoId_TipoId_Vencimiento")
            .IsDescending(false, false, true);

        // Para contar los documentos que usan un tipo al intentar darlo de baja o cambiarle el
        // ámbito (FR-017b, FR-017d).
        tabla.HasIndex(documento => documento.DocumentacionTipoId);

        // Para el panel de vencimientos y el filtro por estado calculado (FR-033, FR-035).
        tabla.HasIndex(documento => documento.FechaVencimiento);

        // Borrar un documento es una operación explícita del operador con confirmación previa, nunca
        // un efecto colateral de borrar otra cosa.
        tabla.HasOne(documento => documento.Vehiculo)
            .WithMany(vehiculo => vehiculo.Documentacion)
            .HasForeignKey(documento => documento.VehiculoId)
            .OnDelete(DeleteBehavior.Restrict);

        // Sin navegación inversa en `DocumentacionTipo`: agregarla sería un tercer cambio al Módulo 3
        // que la spec no autoriza, y el conteo de documentos por tipo se resuelve consultando las dos
        // tablas desde el repositorio (research §2, FR-017b).
        tabla.HasOne(documento => documento.Tipo)
            .WithMany()
            .HasForeignKey(documento => documento.DocumentacionTipoId)
            .OnDelete(DeleteBehavior.Restrict);

        tabla.Ignore(documento => documento.TieneArchivo);
    }
}
