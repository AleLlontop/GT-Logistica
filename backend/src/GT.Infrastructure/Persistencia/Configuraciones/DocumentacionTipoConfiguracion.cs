using GT.Domain.Choferes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class DocumentacionTipoConfiguracion : IEntityTypeConfiguration<DocumentacionTipo>
{
    public void Configure(EntityTypeBuilder<DocumentacionTipo> tabla)
    {
        tabla.ToTable("DocumentacionTipos");
        tabla.HasKey(tipo => tipo.Id);

        tabla.Property(tipo => tipo.Nombre).HasMaxLength(100).IsRequired();
        tabla.Property(tipo => tipo.DiasAvisoVencimiento).IsRequired();
        tabla.Property(tipo => tipo.Activo).IsRequired();

        // Módulo 4, FR-017. La migración le da valor `Chofer` a todas las filas existentes, así que
        // ningún documento ya cargado cambia de comportamiento (FR-017c).
        tabla.Property(tipo => tipo.Ambito)
            .HasConversion<byte>()
            .IsRequired();

        // Sigue siendo único en **todo** el catálogo y no por ámbito: filtrarlo sería un cambio extra
        // al Módulo 3 y la spec pide "nombre único" sin calificarlo (research §3).
        tabla.HasIndex(tipo => tipo.Nombre).IsUnique();
    }
}
