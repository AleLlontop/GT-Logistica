using GT.Domain.Choferes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class DocumentacionConfiguracion : IEntityTypeConfiguration<Documentacion>
{
    public void Configure(EntityTypeBuilder<Documentacion> tabla)
    {
        tabla.ToTable("Documentaciones");
        tabla.HasKey(documento => documento.Id);

        tabla.Property(documento => documento.ChoferId).IsRequired();
        tabla.Property(documento => documento.DocumentacionTipoId).IsRequired();

        // Sin índice único: una licencia conserva su número al renovarse, así que dos documentos del
        // mismo chofer y tipo pueden repetirlo (FR-015).
        tabla.Property(documento => documento.Numero).HasMaxLength(50).IsRequired();

        tabla.Property(documento => documento.FechaEmision).IsRequired();
        tabla.Property(documento => documento.FechaVencimiento).IsRequired();

        tabla.Property(documento => documento.ArchivoRuta).HasMaxLength(400);
        tabla.Property(documento => documento.ArchivoNombre).HasMaxLength(255);
        tabla.Property(documento => documento.ArchivoTipoContenido).HasMaxLength(100);

        // Cubre la ficha del chofer y, sobre todo, la elección del documento vigente de cada tipo:
        // el de vencimiento más lejano, sin ordenar en memoria (FR-020a, research §8).
        tabla.HasIndex(documento => new
            {
                documento.ChoferId,
                documento.DocumentacionTipoId,
                documento.FechaVencimiento,
            })
            .HasDatabaseName("IX_Documentaciones_ChoferId_TipoId_Vencimiento")
            .IsDescending(false, false, true);

        // Para contar los documentos que usan un tipo al intentar darlo de baja (FR-014).
        tabla.HasIndex(documento => documento.DocumentacionTipoId);

        // Para el panel de vencimientos y el filtro por estado calculado (FR-021, FR-022).
        tabla.HasIndex(documento => documento.FechaVencimiento);

        tabla.HasOne(documento => documento.Chofer)
            .WithMany(chofer => chofer.Documentacion)
            .HasForeignKey(documento => documento.ChoferId)
            .OnDelete(DeleteBehavior.Restrict);

        tabla.HasOne(documento => documento.Tipo)
            .WithMany(tipo => tipo.Documentos)
            .HasForeignKey(documento => documento.DocumentacionTipoId)
            .OnDelete(DeleteBehavior.Restrict);

        tabla.Ignore(documento => documento.TieneArchivo);
    }
}
