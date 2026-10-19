using GT.Domain.Liquidaciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class OrdenDePagoConfiguracion : IEntityTypeConfiguration<OrdenDePago>
{
    /// <summary>Secuencia que alimenta el número de orden de pago. La declara <c>GtDbContext</c>.</summary>
    public const string Secuencia = "NumeroDeOrdenDePago";

    public void Configure(EntityTypeBuilder<OrdenDePago> tabla)
    {
        tabla.ToTable(
            "OrdenesDePago",
            constructor =>
            {
                constructor.HasCheckConstraint("CK_OrdenesDePago_Importe", "[Importe] > 0");
                constructor.HasCheckConstraint("CK_OrdenesDePago_Vigencia", "([Vigente]=1 AND [RevertidaPorId] IS NULL AND [RevertidaEn] IS NULL) OR ([Vigente]=0 AND [OrdenPagoProveedorId] IS NOT NULL AND [RevertidaPorId] IS NOT NULL AND [RevertidaEn] IS NOT NULL)");
            });

        tabla.HasKey(orden => orden.Id);

        tabla.Property(orden => orden.Numero)
            .HasDefaultValueSql($"NEXT VALUE FOR dbo.{Secuencia}")
            .ValueGeneratedOnAdd()
            .IsRequired();

        tabla.Property(orden => orden.Vigente).HasDefaultValue(true).HasSentinel(true);
        tabla.HasOne(orden => orden.OrdenPagoProveedor).WithMany().HasForeignKey(orden => orden.OrdenPagoProveedorId).OnDelete(DeleteBehavior.Restrict);
        tabla.HasOne(orden => orden.RevertidaPor).WithMany().HasForeignKey(orden => orden.RevertidaPorId).OnDelete(DeleteBehavior.Restrict);
        tabla.HasIndex(orden => new { orden.OrdenPagoProveedorId, orden.LiquidacionId }).IsUnique()
            .HasFilter("[OrdenPagoProveedorId] IS NOT NULL").HasDatabaseName("IX_OP_Origen_Liquidacion");
        tabla.Property(orden => orden.LiquidacionId).IsRequired();
        tabla.Property(orden => orden.FechaPago).IsRequired();
        tabla.Property(orden => orden.Importe).HasColumnType("decimal(18,2)").IsRequired();
        tabla.Property(orden => orden.UsuarioId).IsRequired();
        tabla.Property(orden => orden.RegistradaEn).IsRequired();

        tabla.HasIndex(orden => orden.Numero).IsUnique().HasDatabaseName("IX_OrdenesDePago_Numero");
        tabla.HasIndex(orden => orden.LiquidacionId).HasDatabaseName("IX_OrdenesDePago_LiquidacionId");

        tabla.HasOne(orden => orden.Liquidacion)
            .WithMany(liquidacion => liquidacion.OrdenesDePago)
            .HasForeignKey(orden => orden.LiquidacionId)
            .OnDelete(DeleteBehavior.Restrict);

        // El Módulo 2 usa baja lógica: la orden sigue diciendo quién la cargó aunque la cuenta ya no opere.
        tabla.HasOne(orden => orden.Usuario)
            .WithMany()
            .HasForeignKey(orden => orden.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
