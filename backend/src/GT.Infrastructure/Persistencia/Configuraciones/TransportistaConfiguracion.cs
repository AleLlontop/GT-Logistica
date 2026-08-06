using GT.Domain.Choferes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class TransportistaConfiguracion : IEntityTypeConfiguration<Transportista>
{
    public void Configure(EntityTypeBuilder<Transportista> tabla)
    {
        tabla.ToTable("Transportistas");
        tabla.HasKey(transportista => transportista.Id);

        tabla.Property(transportista => transportista.Nombre).HasMaxLength(200).IsRequired();
        tabla.Property(transportista => transportista.Cuit).HasMaxLength(11).IsRequired();
        tabla.Property(transportista => transportista.Tipo).HasConversion<byte>().IsRequired();
        tabla.Property(transportista => transportista.Telefono).HasMaxLength(30).IsRequired();
        tabla.Property(transportista => transportista.Email).HasMaxLength(254).IsRequired();
        tabla.Property(transportista => transportista.Activo).IsRequired();

        // La unicidad del CUIT se garantiza en la base y no sólo en la validación previa, para que
        // dos altas simultáneas no puedan registrar dos veces al mismo transportista (FR-003).
        tabla.HasIndex(transportista => transportista.Cuit).IsUnique();
    }
}
