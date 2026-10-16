using GT.Domain.Proveedores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class ProveedorConfiguracion : IEntityTypeConfiguration<Proveedor>
{
    public void Configure(EntityTypeBuilder<Proveedor> tabla)
    {
        tabla.ToTable("Proveedores", t =>
        {
            t.HasCheckConstraint("CK_Proveedores_Tipo", "[Tipo] IN (0, 1)");
            t.HasCheckConstraint("CK_Proveedores_Version", "[Version] > 0");
            t.HasCheckConstraint("CK_Proveedores_Identidad",
                "([Tipo] = 0 AND [TransportistaId] IS NOT NULL AND [RazonSocial] IS NULL AND [Cuit] IS NULL AND [Telefono] IS NULL AND [Email] IS NULL) OR " +
                "([Tipo] = 1 AND [TransportistaId] IS NULL AND [RazonSocial] IS NOT NULL AND LEN(LTRIM(RTRIM([RazonSocial]))) > 0 AND [Cuit] IS NOT NULL AND LEN([Cuit]) = 11)");
        });
        tabla.HasKey(p => p.Id);
        tabla.Property(p => p.Tipo).HasConversion<int>();
        tabla.Property(p => p.Activo).HasDefaultValue(true).HasSentinel(true);
        tabla.Property(p => p.Version).IsConcurrencyToken();
        tabla.Property(p => p.RazonSocial).HasMaxLength(200);
        tabla.Property(p => p.Cuit).HasMaxLength(11).IsUnicode(false);
        tabla.Property(p => p.Telefono).HasMaxLength(30);
        tabla.Property(p => p.Email).HasMaxLength(254);
        tabla.Property(p => p.Direccion).HasMaxLength(200);
        tabla.Property(p => p.NombreComercial).HasColumnType("nvarchar(max)");
        tabla.HasOne(p => p.Transportista).WithMany().HasForeignKey(p => p.TransportistaId).OnDelete(DeleteBehavior.Restrict);
        tabla.HasIndex(p => p.Cuit).IsUnique().HasFilter("[Cuit] IS NOT NULL");
        tabla.HasIndex(p => p.TransportistaId).IsUnique().HasFilter("[TransportistaId] IS NOT NULL");
    }
}
