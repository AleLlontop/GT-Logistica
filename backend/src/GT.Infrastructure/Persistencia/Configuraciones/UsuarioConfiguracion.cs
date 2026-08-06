using GT.Domain.Usuarios;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class UsuarioConfiguracion : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> tabla)
    {
        tabla.ToTable("Usuarios");
        tabla.HasKey(usuario => usuario.Id);

        tabla.Property(usuario => usuario.Username).HasMaxLength(50).IsRequired();
        tabla.Property(usuario => usuario.UsernameNormalizado).HasMaxLength(50).IsRequired();
        tabla.Property(usuario => usuario.Email).HasMaxLength(254).IsRequired();
        tabla.Property(usuario => usuario.EmailNormalizado).HasMaxLength(254).IsRequired();
        tabla.Property(usuario => usuario.PasswordHash).HasMaxLength(200).IsRequired();
        tabla.Property(usuario => usuario.Estado).HasConversion<byte>().IsRequired();
        tabla.Property(usuario => usuario.FechaAlta).IsRequired();
        tabla.Property(usuario => usuario.PasswordActualizadaEn).IsRequired();

        // La unicidad del username se garantiza en la base, no sólo en la validación previa,
        // para que dos altas simultáneas no puedan crear el mismo usuario (FR-002).
        tabla.HasIndex(usuario => usuario.UsernameNormalizado).IsUnique();

        // Lo mismo para el email (FR-003).
        tabla.HasIndex(usuario => usuario.EmailNormalizado).IsUnique();

        // Índice único FILTRADO: sin el filtro, SQL Server trataría como duplicados a los varios
        // `NULL` y sólo un usuario en todo el sistema podría quedarse sin persona asociada — un caso
        // que la spec declara válido y habitual (FR-008, research §5).
        tabla
            .HasIndex(usuario => usuario.PersonaId)
            .IsUnique()
            .HasFilter("[PersonaId] IS NOT NULL");

        tabla
            .HasOne(usuario => usuario.Persona)
            .WithOne()
            .HasForeignKey<Usuario>(usuario => usuario.PersonaId)
            .OnDelete(DeleteBehavior.Restrict);

        tabla
            .HasMany(usuario => usuario.Roles)
            .WithMany(rol => rol.Usuarios)
            .UsingEntity(union => union.ToTable("UsuarioRoles"));

        tabla.Ignore(usuario => usuario.PuedeAutenticarse);
        tabla.Ignore(usuario => usuario.PermisosEfectivos);
    }
}
