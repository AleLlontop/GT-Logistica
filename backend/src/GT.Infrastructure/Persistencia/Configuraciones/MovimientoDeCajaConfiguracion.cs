using GT.Domain.Caja;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class MovimientoDeCajaConfiguracion : IEntityTypeConfiguration<MovimientoDeCaja>
{
    public const string CheckImporte = "CK_MovimientosDeCaja_Importe";

    public const string CheckConcepto = "CK_MovimientosDeCaja_Concepto";

    public const string CheckReferencia = "CK_MovimientosDeCaja_Referencia";

    public void Configure(EntityTypeBuilder<MovimientoDeCaja> tabla)
    {
        tabla.ToTable("MovimientosDeCaja", constructor =>
        {
            // RN3 como garantía de la base. Los dos decimales los valida la aplicación.
            constructor.HasCheckConstraint(CheckImporte, "[Importe] > 0");

            // RN5: un movimiento no se edita (FR-014), así que un concepto en blanco que llegara por otro
            // camino quedaría así para siempre (research §7). La columna no admite nulo: no hace falta el
            // `IS NOT NULL` de la convención [010].
            constructor.HasCheckConstraint(CheckConcepto, "LEN(LTRIM(RTRIM([Concepto]))) > 0");

            // RN8: la referencia sigue al tipo, también para quien invoque el repositorio sin el caso de uso.
            //
            // ⚠ 0 y 1 son `TipoMovimientoCaja` escrito a mano (research §6).
            constructor.HasCheckConstraint(
                CheckReferencia,
                "([Tipo] = 0 AND [OrdenDePagoId] IS NULL) OR ([Tipo] = 1 AND [FacturaId] IS NULL)");
        });

        tabla.HasKey(movimiento => movimiento.Id);

        tabla.Property(movimiento => movimiento.Tipo).HasConversion<byte>().IsRequired();
        tabla.Property(movimiento => movimiento.Importe).HasColumnType("decimal(18,2)").IsRequired();
        tabla.Property(movimiento => movimiento.Concepto).HasMaxLength(ReglasDeCaja.LargoMaximoDelConcepto).IsRequired();
        tabla.Property(movimiento => movimiento.Fecha).IsRequired();

        // Los movimientos de una caja, en orden: el detalle y el resumen de cierre.
        tabla.HasIndex(movimiento => new { movimiento.CajaId, movimiento.Fecha })
            .HasDatabaseName("IX_MovimientosDeCaja_Caja_Fecha");

        // El rango de fechas de la consulta global (FR-023).
        tabla.HasIndex(movimiento => movimiento.Fecha).HasDatabaseName("IX_MovimientosDeCaja_Fecha");

        tabla.HasOne(movimiento => movimiento.Caja)
            .WithMany(caja => caja.Movimientos)
            .HasForeignKey(movimiento => movimiento.CajaId)
            .OnDelete(DeleteBehavior.Restrict);

        tabla.HasOne(movimiento => movimiento.Usuario)
            .WithMany()
            .HasForeignKey(movimiento => movimiento.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);

        // `WithMany()` sin colección y **sin índice único**: la misma factura o la misma orden se puede
        // referenciar desde más de un movimiento, y los Módulos 6 y 9 no ganan navegación (FR-012).
        tabla.HasOne(movimiento => movimiento.Factura)
            .WithMany()
            .HasForeignKey(movimiento => movimiento.FacturaId)
            .OnDelete(DeleteBehavior.Restrict);

        tabla.HasOne(movimiento => movimiento.OrdenDePago)
            .WithMany()
            .HasForeignKey(movimiento => movimiento.OrdenDePagoId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
