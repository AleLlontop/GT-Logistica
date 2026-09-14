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
            constructor => constructor.HasCheckConstraint("CK_OrdenesDePago_Importe", "[Importe] > 0"));

        tabla.HasKey(orden => orden.Id);

        tabla.Property(orden => orden.Numero)
            .HasDefaultValueSql($"NEXT VALUE FOR dbo.{Secuencia}")
            .ValueGeneratedOnAdd()
            .IsRequired();

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
