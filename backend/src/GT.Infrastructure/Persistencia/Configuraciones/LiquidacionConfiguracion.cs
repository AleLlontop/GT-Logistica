using GT.Domain.Liquidaciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class LiquidacionConfiguracion : IEntityTypeConfiguration<Liquidacion>
{
    /// <summary>Secuencia que alimenta el número de liquidación. La declara <c>GtDbContext</c>.</summary>
    public const string Secuencia = "NumeroDeLiquidacion";

    public const string IndiceNumero = "IX_Liquidaciones_Numero";

    public const string CheckEstado = "CK_Liquidaciones_Estado";

    public void Configure(EntityTypeBuilder<Liquidacion> tabla)
    {
        tabla.ToTable("Liquidaciones", constructor =>
        {
            // El año no lleva CHECK, igual que `Facturas.PeriodoAnio`: la lista crece con los años.
            constructor.HasCheckConstraint("CK_Liquidaciones_PeriodoMes", "[PeriodoMes] BETWEEN 1 AND 12");

            // FR-007, FR-012a: no hay liquidación en $ 0.
            constructor.HasCheckConstraint("CK_Liquidaciones_ImporteTotal", "[ImporteTotal] > 0");

            // FR-043 como garantía de la base y no como validación que alguien puede saltear.
            constructor.HasCheckConstraint(
                "CK_Liquidaciones_ImportePagado",
                "[ImportePagado] >= 0 AND [ImportePagado] <= [ImporteTotal]");

            // Lo que vuelve seguro **guardar** `pagada` en vez de derivarla (research §3): la base rechaza
            // una pagada con saldo, una pendiente sin saldo y una anulada con pagos o sin motivo.
            //
            // ⚠ 0, 1 y 2 son `EstadoLiquidacion` escrito a mano: reordenar el enum no falla al compilar y
            // deja este CHECK protegiendo el estado equivocado. Lo cubre `RestriccionesDeLiquidacionTests`.
            constructor.HasCheckConstraint(
                CheckEstado,
                "([Estado] = 0 AND [ImportePagado] < [ImporteTotal] AND [MotivoAnulacion] IS NULL) OR " +
                "([Estado] = 1 AND [ImportePagado] = [ImporteTotal] AND [MotivoAnulacion] IS NULL) OR " +
                "([Estado] = 2 AND [ImportePagado] = 0 AND [MotivoAnulacion] IS NOT NULL)");
        });

        tabla.HasKey(liquidacion => liquidacion.Id);

        // El valor lo pone el DEFAULT de la columna. `ValueGeneratedOnAdd` es lo que hace que EF omita la
        // columna en el INSERT; sin eso mandaría el 0 y la secuencia no se aplicaría nunca (research §12.2).
        tabla.Property(liquidacion => liquidacion.Numero)
            .HasDefaultValueSql($"NEXT VALUE FOR dbo.{Secuencia}")
            .ValueGeneratedOnAdd()
            .IsRequired();

        tabla.Property(liquidacion => liquidacion.TransportistaId).IsRequired();
        tabla.Property(liquidacion => liquidacion.PeriodoMes).IsRequired();
        tabla.Property(liquidacion => liquidacion.PeriodoAnio).IsRequired();

        // `decimal`, nunca punto flotante (convención [005]).
        tabla.Property(liquidacion => liquidacion.ImporteTotal).HasColumnType("decimal(18,2)").IsRequired();
        tabla.Property(liquidacion => liquidacion.ImportePagado)
            .HasColumnType("decimal(18,2)")
            .HasDefaultValue(0m)
            .IsRequired();

        tabla.Property(liquidacion => liquidacion.Estado)
            .HasConversion<byte>()
            .HasDefaultValue(EstadoLiquidacion.Pendiente)
            .IsRequired();

        tabla.Property(liquidacion => liquidacion.MotivoAnulacion).HasMaxLength(500);
        tabla.Property(liquidacion => liquidacion.Version).HasDefaultValue(0).IsRequired();

        // FR-016 y el orden del listado (FR-023): la secuencia crece con cada generación, así que el número
        // más alto es la más reciente, y como es único el orden es total sin desempate (convención [003]).
        tabla.HasIndex(liquidacion => liquidacion.Numero)
            .IsUnique()
            .IsDescending(true)
            .HasDatabaseName(IndiceNumero);

        tabla.HasIndex(liquidacion => liquidacion.TransportistaId)
            .HasDatabaseName("IX_Liquidaciones_TransportistaId");

        tabla.HasIndex(liquidacion => new { liquidacion.PeriodoAnio, liquidacion.PeriodoMes })
            .HasDatabaseName("IX_Liquidaciones_Periodo");

        tabla.HasIndex(liquidacion => liquidacion.Estado).HasDatabaseName("IX_Liquidaciones_Estado");

        // Sin navegación inversa: el Módulo 3 no se modifica, ni siquiera con una colección. La
        // liquidación conoce al transportista; el transportista no sabe de liquidaciones.
        tabla.HasOne(liquidacion => liquidacion.Transportista)
            .WithMany()
            .HasForeignKey(liquidacion => liquidacion.TransportistaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
