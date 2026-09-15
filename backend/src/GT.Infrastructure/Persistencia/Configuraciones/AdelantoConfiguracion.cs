using GT.Domain.Adelantos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class AdelantoConfiguracion : IEntityTypeConfiguration<Adelanto>
{
    public const string CheckTipoBeneficiario = "CK_Adelantos_TipoBeneficiario";

    public const string CheckImporte = "CK_Adelantos_Importe";

    public const string CheckMotivo = "CK_Adelantos_Motivo";

    public const string CheckEstado = "CK_Adelantos_Estado";

    public void Configure(EntityTypeBuilder<Adelanto> tabla)
    {
        tabla.ToTable("Adelantos", constructor =>
        {
            // ⚠ 1 y 2 son `TipoBeneficiario` escrito a mano. Lo cubre `RestriccionesDeAdelantoTests`.
            constructor.HasCheckConstraint(CheckTipoBeneficiario, "[TipoBeneficiario] IN (1, 2)");

            // FR-007 como garantía de la base. Los dos decimales los valida la aplicación.
            constructor.HasCheckConstraint(CheckImporte, "[Importe] > 0");

            // FR-006: el motivo llega recortado, así que sólo espacios queda en largo cero.
            constructor.HasCheckConstraint(CheckMotivo, "LEN([Motivo]) > 0");

            // FR-024 y FR-029 —rechazo y anulación exigen motivo— y la exclusión entre los dos motivos,
            // como garantías del dato y no del código (research §3).
            //
            // ⚠ 0, 1, 2 y 3 son `EstadoAdelanto` escrito a mano: reordenar el enum no falla al compilar.
            // ⚠ Cada `LEN` va detrás de su `IS NOT NULL`: `LEN(NULL) > 0` es UNKNOWN, y un CHECK sólo
            //   rechaza FALSE. Sin el `IS NOT NULL`, un rechazado sin motivo pasaría (research §9.2).
            constructor.HasCheckConstraint(
                CheckEstado,
                "([Estado] IN (0, 1) AND [MotivoRechazo] IS NULL AND [MotivoAnulacion] IS NULL) OR " +
                "([Estado] = 2 AND [MotivoRechazo] IS NOT NULL AND LEN([MotivoRechazo]) > 0 AND [MotivoAnulacion] IS NULL) OR " +
                "([Estado] = 3 AND [MotivoAnulacion] IS NOT NULL AND LEN([MotivoAnulacion]) > 0 AND [MotivoRechazo] IS NULL)");
        });

        tabla.HasKey(adelanto => adelanto.Id);

        tabla.Property(adelanto => adelanto.PersonaId).IsRequired();
        tabla.Property(adelanto => adelanto.TipoBeneficiario).HasConversion<byte>().IsRequired();
        tabla.Property(adelanto => adelanto.Fecha).IsRequired();
        tabla.Property(adelanto => adelanto.Motivo).HasMaxLength(200).IsRequired();

        // `decimal`, nunca punto flotante (convención [005]).
        tabla.Property(adelanto => adelanto.Importe).HasColumnType("decimal(18,2)").IsRequired();

        // **Sin `HasDefaultValue`** a propósito: la entidad nace pendiente y EF manda siempre el valor. Con
        // un default de base igual al del CLR, EF omitiría el 0 del INSERT sin que nada falle
        // (convención [009] sobre `HasSentinel`).
        tabla.Property(adelanto => adelanto.Estado).HasConversion<byte>().IsRequired();

        tabla.Property(adelanto => adelanto.MotivoRechazo).HasMaxLength(500);
        tabla.Property(adelanto => adelanto.MotivoAnulacion).HasMaxLength(500);

        // Orden del listado (FR-017) y rango de fechas (FR-014). El `Id` desempata a igual fecha: sin él el
        // orden no es total y una fila puede repetirse entre páginas (convención [003]).
        tabla.HasIndex(adelanto => new { adelanto.Fecha, adelanto.Id })
            .IsDescending(true, true)
            .HasDatabaseName("IX_Adelantos_Fecha");

        tabla.HasIndex(adelanto => adelanto.PersonaId).HasDatabaseName("IX_Adelantos_PersonaId");
        tabla.HasIndex(adelanto => adelanto.Estado).HasDatabaseName("IX_Adelantos_Estado");

        // Sin navegación inversa: el Módulo 2 no se modifica, ni siquiera con una colección.
        tabla.HasOne(adelanto => adelanto.Persona)
            .WithMany()
            .HasForeignKey(adelanto => adelanto.PersonaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
