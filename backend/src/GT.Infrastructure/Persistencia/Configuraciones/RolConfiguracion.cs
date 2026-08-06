using GT.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class RolConfiguracion : IEntityTypeConfiguration<Rol>
{
    public void Configure(EntityTypeBuilder<Rol> tabla)
    {
        tabla.ToTable("Roles");
        tabla.HasKey(rol => rol.Id);

        tabla.Property(rol => rol.Codigo).HasMaxLength(50).IsRequired();
        tabla.Property(rol => rol.Nombre).HasMaxLength(100).IsRequired();

        tabla.HasIndex(rol => rol.Codigo).IsUnique();

        tabla
            .HasMany(rol => rol.Permisos)
            .WithMany(permiso => permiso.Roles)
            .UsingEntity(union => union.ToTable("RolPermisos"));
    }
}
