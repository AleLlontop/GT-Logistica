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

        tabla.HasIndex(tipo => tipo.Nombre).IsUnique();
    }
}
