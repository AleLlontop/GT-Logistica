using GT.Domain.Liquidaciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class CambioDeLiquidacionConfiguracion : IEntityTypeConfiguration<CambioDeLiquidacion>
{
    public const string IndiceUnica = "IX_CambiosDeLiquidacion_Unica";

    public void Configure(EntityTypeBuilder<CambioDeLiquidacion> tabla)
    {
        tabla.ToTable("CambiosDeLiquidacion");
        tabla.HasKey(cambio => cambio.Id);

        tabla.Property(cambio => cambio.LiquidacionId).IsRequired();
        tabla.Property(cambio => cambio.Operacion).HasConversion<byte>().IsRequired();
        tabla.Property(cambio => cambio.UsuarioId).IsRequired();
        tabla.Property(cambio => cambio.OcurridoEn).IsRequired();

        // Una sola generación, una sola anulación y un solo paso a pagada por liquidación; ediciones, las
        // que hagan falta. Es además lo que garantiza que la fecha de generación sea una sola.
        //
        // ⚠ El 1 es `OperacionDeLiquidacion.Edicion` escrito a mano.
        tabla.HasIndex(cambio => new { cambio.LiquidacionId, cambio.Operacion })
            .IsUnique()
            .HasFilter("[Operacion] <> 1")
            .HasDatabaseName(IndiceUnica);

        tabla.HasOne(cambio => cambio.Liquidacion)
            .WithMany(liquidacion => liquidacion.Cambios)
            .HasForeignKey(cambio => cambio.LiquidacionId)
            .OnDelete(DeleteBehavior.Restrict);

        tabla.HasOne(cambio => cambio.Usuario)
            .WithMany()
            .HasForeignKey(cambio => cambio.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
