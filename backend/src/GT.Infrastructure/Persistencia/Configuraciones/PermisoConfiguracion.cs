using GT.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class PermisoConfiguracion : IEntityTypeConfiguration<Permiso>
{
    public void Configure(EntityTypeBuilder<Permiso> tabla)
    {
        tabla.ToTable("Permisos");
        tabla.HasKey(permiso => permiso.Id);

        tabla.Property(permiso => permiso.Codigo).HasMaxLength(100).IsRequired();
        tabla.Property(permiso => permiso.Modulo).HasMaxLength(50).IsRequired();
        tabla.Property(permiso => permiso.Descripcion).HasMaxLength(200).IsRequired();

        tabla.HasIndex(permiso => permiso.Codigo).IsUnique();
    }
}
