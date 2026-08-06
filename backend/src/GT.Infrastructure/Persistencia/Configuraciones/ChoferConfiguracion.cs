using GT.Domain.Choferes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class ChoferConfiguracion : IEntityTypeConfiguration<Chofer>
{
    public void Configure(EntityTypeBuilder<Chofer> tabla)
    {
        tabla.ToTable("Choferes");
        tabla.HasKey(chofer => chofer.Id);

        tabla.Property(chofer => chofer.Cuil).HasMaxLength(11).IsRequired();
        tabla.Property(chofer => chofer.PersonaId).IsRequired();
        tabla.Property(chofer => chofer.TransportistaId).IsRequired();
        tabla.Property(chofer => chofer.Activo).IsRequired();

        // Una persona es chofer a lo sumo una vez, activo o inactivo. A diferencia del índice de
        // `Usuarios.PersonaId` del Módulo 2, éste no lleva filtro: acá la columna es obligatoria, así
        // que no hay NULL que SQL Server pueda confundir entre sí.
        tabla.HasIndex(chofer => chofer.PersonaId).IsUnique();

        // Unicidad del CUIL garantizada en la base (FR-007).
        tabla.HasIndex(chofer => chofer.Cuil).IsUnique();

        // Para el filtro por transportista del listado (FR-022).
        tabla.HasIndex(chofer => chofer.TransportistaId);

        tabla.HasOne(chofer => chofer.Persona)
            .WithOne(persona => persona.Chofer)
            .HasForeignKey<Chofer>(chofer => chofer.PersonaId)
            .OnDelete(DeleteBehavior.Restrict);

        tabla.HasOne(chofer => chofer.Transportista)
            .WithMany(transportista => transportista.Choferes)
            .HasForeignKey(chofer => chofer.TransportistaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
