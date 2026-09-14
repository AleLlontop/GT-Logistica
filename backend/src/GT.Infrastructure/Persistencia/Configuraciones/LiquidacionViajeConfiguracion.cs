using GT.Domain.Liquidaciones;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class LiquidacionViajeConfiguracion : IEntityTypeConfiguration<LiquidacionViaje>
{
    /// <summary>
    /// El índice que garantiza que un viaje no esté en dos liquidaciones vigentes (FR-014, SC-002).
    /// <c>RepositorioLiquidaciones</c> lo busca por nombre en el mensaje de SQL Server para traducir la
    /// carrera al rechazo que corresponde (research §12.7).
    /// </summary>
    public const string IndiceViajeVigente = "IX_LiquidacionViajes_ViajeVigente";

    public void Configure(EntityTypeBuilder<LiquidacionViaje> tabla)
    {
        tabla.ToTable("LiquidacionViajes");
        tabla.HasKey(vinculo => new { vinculo.LiquidacionId, vinculo.ViajeId });

        // `HasSentinel(true)` porque el valor por defecto de la base coincide con el del constructor:
        // sin él EF tomaría `false` como "sin asignar", lo omitiría en el INSERT y la base guardaría
        // `true`. Con el centinela en `true` es al revés, y `false` viaja siempre.
        tabla.Property(vinculo => vinculo.Vigente)
            .HasDefaultValue(true)
            .HasSentinel(true)
            .IsRequired();

        // La garantía real, no una optimización. La consulta previa da el mensaje bueno; el índice cierra
        // la carrera entre dos operadores simultáneos (convención [005]).
        tabla.HasIndex(vinculo => vinculo.ViajeId)
            .IsUnique()
            .HasFilter("[Vigente] = 1")
            .HasDatabaseName(IndiceViajeVigente);

        tabla.HasOne(vinculo => vinculo.Liquidacion)
            .WithMany(liquidacion => liquidacion.Viajes)
            .HasForeignKey(vinculo => vinculo.LiquidacionId)
            .OnDelete(DeleteBehavior.Restrict);

        // `WithMany()` sin colección: `Viaje` no gana navegación y el Módulo 5 no se toca (research §1).
        tabla.HasOne(vinculo => vinculo.Viaje)
            .WithMany()
            .HasForeignKey(vinculo => vinculo.ViajeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
