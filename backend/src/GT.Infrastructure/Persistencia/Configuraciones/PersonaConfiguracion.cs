using GT.Domain.Personas;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class PersonaConfiguracion : IEntityTypeConfiguration<Persona>
{
    public void Configure(EntityTypeBuilder<Persona> tabla)
    {
        tabla.ToTable("Personas");
        tabla.HasKey(persona => persona.Id);

        tabla.Property(persona => persona.Nombre).HasMaxLength(100).IsRequired();
        tabla.Property(persona => persona.Apellido).HasMaxLength(100).IsRequired();
        tabla.Property(persona => persona.Dni).HasMaxLength(15).IsRequired();
        tabla.Property(persona => persona.Tipo).HasConversion<byte>().IsRequired();
        tabla.Property(persona => persona.Telefono).HasMaxLength(30).IsRequired();
        tabla.Property(persona => persona.Email).HasMaxLength(254).IsRequired();
        tabla.Property(persona => persona.FechaNacimiento).IsRequired();
        tabla.Property(persona => persona.Activa).IsRequired();

        // La unicidad del DNI se garantiza en la base, no sólo en la validación previa, para que dos
        // altas simultáneas no puedan registrar dos veces a la misma persona (FR-027).
        tabla.HasIndex(persona => persona.Dni).IsUnique();

        tabla.Ignore(persona => persona.NombreCompleto);
    }
}
