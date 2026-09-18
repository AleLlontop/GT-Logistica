using GT.Domain.Caja;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class CajaConfiguracion : IEntityTypeConfiguration<Caja>
{
    public const string CheckSaldoInicial = "CK_Cajas_SaldoInicial";

    public const string CheckCierreConsistente = "CK_Cajas_CierreConsistente";

    /// <summary>
    /// RN1: una caja abierta por empleado (FR-003, FR-004). <c>RepositorioCaja</c> lo busca por nombre en el
    /// mensaje de SQL Server para traducir la carrera del doble clic al rechazo que corresponde (research §1).
    /// </summary>
    public const string IndiceResponsableAbierta = "IX_Cajas_UsuarioResponsable_Abierta";

    public void Configure(EntityTypeBuilder<Caja> tabla)
    {
        tabla.ToTable("Cajas", constructor =>
        {
            // RN4 como garantía de la base. Los dos decimales los valida la aplicación.
            constructor.HasCheckConstraint(CheckSaldoInicial, "[SaldoInicial] >= 0");

            // El estado atado a las dos columnas de las que depende (convención [009]): un cierre a medio
            // hacer es imposible en la base, no sólo en el código.
            //
            // ⚠ 0 y 1 son `EstadoCaja` escrito a mano: reordenar el enum no falla al compilar. Lo cubre
            //   `RestriccionesDeCajaTests`.
            constructor.HasCheckConstraint(
                CheckCierreConsistente,
                "([Estado] = 0 AND [FechaCierre] IS NULL AND [SaldoFinal] IS NULL) OR " +
                "([Estado] = 1 AND [FechaCierre] IS NOT NULL AND [SaldoFinal] IS NOT NULL)");
        });

        tabla.HasKey(caja => caja.Id);

        // `decimal`, nunca punto flotante (convención [005]).
        tabla.Property(caja => caja.SaldoInicial).HasColumnType("decimal(18,2)").IsRequired();
        tabla.Property(caja => caja.SaldoFinal).HasColumnType("decimal(18,2)");

        tabla.Property(caja => caja.FechaApertura).IsRequired();

        // **Sin `HasDefaultValue`** a propósito: la entidad nace abierta y EF manda siempre el valor. Con un
        // default de base igual al del CLR, EF omitiría el 0 del INSERT sin que nada falle (convención [009]
        // sobre `HasSentinel`).
        tabla.Property(caja => caja.Estado).HasConversion<byte>().IsRequired();

        // La garantía real de RN1, no una optimización: la consulta previa da el mensaje bueno y el índice
        // cierra la carrera (convención [005]).
        //
        // ⚠ 0 es `EstadoCaja.Abierta` escrito a mano.
        tabla.HasIndex(caja => caja.UsuarioResponsableId)
            .IsUnique()
            .HasFilter("[Estado] = 0")
            .HasDatabaseName(IndiceResponsableAbierta);

        // Orden del listado (FR-026). El `Id` desempata a igual apertura (convención [003]).
        tabla.HasIndex(caja => new { caja.FechaApertura, caja.Id })
            .IsDescending(true, true)
            .HasDatabaseName("IX_Cajas_FechaApertura");

        // Sin navegación inversa: el Módulo 2 no se modifica, ni siquiera con una colección.
        tabla.HasOne(caja => caja.UsuarioResponsable)
            .WithMany()
            .HasForeignKey(caja => caja.UsuarioResponsableId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
