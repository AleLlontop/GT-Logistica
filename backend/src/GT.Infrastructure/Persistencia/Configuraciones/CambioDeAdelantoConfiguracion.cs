using GT.Domain.Adelantos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class CambioDeAdelantoConfiguracion : IEntityTypeConfiguration<CambioDeAdelanto>
{
    public const string IndiceOperacion = "IX_CambiosDeAdelanto_Operacion";

    public void Configure(EntityTypeBuilder<CambioDeAdelanto> tabla)
    {
        tabla.ToTable("CambiosDeAdelanto");
        tabla.HasKey(cambio => cambio.Id);

        tabla.Property(cambio => cambio.AdelantoId).IsRequired();
        tabla.Property(cambio => cambio.Operacion).HasConversion<byte>().IsRequired();
        tabla.Property(cambio => cambio.UsuarioId).IsRequired();
        tabla.Property(cambio => cambio.OcurridoEn).IsRequired();

        // Cada operación a lo sumo una vez por adelanto: las transiciones de FR-034 no repiten ninguna. Es
        // la segunda red, no la primera: la carrera la cierra el `UPDATE` condicional (research §2). Sin
        // filtro, así que no lleva ningún valor de enum escrito a mano.
        tabla.HasIndex(cambio => new { cambio.AdelantoId, cambio.Operacion })
            .IsUnique()
            .HasDatabaseName(IndiceOperacion);

        tabla.HasOne(cambio => cambio.Adelanto)
            .WithMany(adelanto => adelanto.Cambios)
            .HasForeignKey(cambio => cambio.AdelantoId)
            .OnDelete(DeleteBehavior.Restrict);

        tabla.HasOne(cambio => cambio.Usuario)
            .WithMany()
            .HasForeignKey(cambio => cambio.UsuarioId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
