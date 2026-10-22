using GT.Domain.Proveedores;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class FacturaProveedorConfiguracion : IEntityTypeConfiguration<FacturaProveedor>
{
    public void Configure(EntityTypeBuilder<FacturaProveedor> tabla)
    {
        tabla.ToTable("FacturasProveedor", t =>
        {
            t.HasCheckConstraint("CK_FacturasProveedor_Importes", "[Neto] > 0 AND [Iva] = ROUND([Neto] * 0.21, 2) AND [ImporteTotal] = [Neto] + [Iva]");
            t.HasCheckConstraint("CK_FacturasProveedor_Fechas", "[FechaVencimientoCae] >= [Fecha]");
            t.HasCheckConstraint("CK_FacturasProveedor_Estado", "[Estado] IN (0,1)");
            t.HasCheckConstraint("CK_FacturasProveedor_Textos", "LEN(LTRIM(RTRIM([Numero]))) > 0 AND LEN(LTRIM(RTRIM([Detalle]))) > 0 AND LEN(LTRIM(RTRIM([Cae]))) > 0");
            t.HasCheckConstraint("CK_FacturasProveedor_Liquidacion",
                "([TipoProveedorAlRegistrar] = 0 AND [LiquidacionId] IS NOT NULL AND [ImporteLiquidacionAlRegistrar] IS NOT NULL AND [ImporteLiquidacionAlRegistrar] = [ImporteTotal]) OR " +
                "([TipoProveedorAlRegistrar] = 1 AND [LiquidacionId] IS NULL AND [ImporteLiquidacionAlRegistrar] IS NULL)");
            t.HasCheckConstraint("CK_FacturasProveedor_Archivo",
                "([ArchivoRuta] IS NULL AND [ArchivoNombre] IS NULL AND [ArchivoTipoContenido] IS NULL) OR " +
                "([ArchivoRuta] IS NOT NULL AND LEN([ArchivoRuta]) > 0 AND [ArchivoNombre] IS NOT NULL AND LEN([ArchivoNombre]) > 0 AND [ArchivoTipoContenido] IS NOT NULL AND [ArchivoTipoContenido] = N'application/pdf')");
        });
        tabla.HasKey(f => f.Id);
        tabla.Property(f => f.TipoProveedorAlRegistrar).HasConversion<int>();
        tabla.Property(f => f.Estado).HasConversion<int>();
        tabla.Property(f => f.ProveedorRazonSocial).HasMaxLength(200).IsRequired();
        tabla.Property(f => f.ProveedorCuit).HasMaxLength(11).IsUnicode(false).IsRequired();
        tabla.Property(f => f.Numero).HasMaxLength(400).IsRequired();
        tabla.Property(f => f.Detalle).IsRequired();
        tabla.Property(f => f.Cae).IsRequired();
        tabla.Property(f => f.Neto).HasPrecision(18, 2);
        tabla.Property(f => f.Iva).HasPrecision(18, 2);
        tabla.Property(f => f.ImporteTotal).HasPrecision(18, 2);
        tabla.Property(f => f.ImporteLiquidacionAlRegistrar).HasPrecision(18, 2);
        tabla.Property(f => f.ArchivoRuta).HasMaxLength(400);
        tabla.Property(f => f.ArchivoNombre).HasMaxLength(255);
        tabla.Property(f => f.ArchivoTipoContenido).HasMaxLength(100);
        tabla.HasOne(f => f.Proveedor).WithMany().HasForeignKey(f => f.ProveedorId).OnDelete(DeleteBehavior.Restrict);
        tabla.HasOne(f => f.Liquidacion).WithMany().HasForeignKey(f => f.LiquidacionId).OnDelete(DeleteBehavior.Restrict);
        tabla.HasIndex(f => new { f.ProveedorId, f.Numero }).IsUnique();
        tabla.HasIndex(f => f.LiquidacionId).IsUnique().HasFilter("[LiquidacionId] IS NOT NULL");
        tabla.HasIndex(f => new { f.Fecha, f.Id }).IsDescending(true, true);
    }
}
