using GT.Domain.Viajes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class ViajeConfiguracion : IEntityTypeConfiguration<Viaje>
{
    /// <summary>
    /// Nombres de los tres índices únicos. Los repositorios los leen del mensaje de la excepción de
    /// SQL Server para saber **cuál** se violó y traducirlo al rechazo que corresponde: sin eso, una
    /// carrera por el remito y una por el chofer llegarían con el mismo error (convención [003]).
    /// </summary>
    public const string IndiceNumero = "IX_Viajes_Numero";

    public const string IndiceRemito = "IX_Viajes_NumeroRemito";

    public const string IndiceChoferEnCurso = "IX_Viajes_ChoferEnCurso";

    public const string IndiceVehiculoEnCurso = "IX_Viajes_VehiculoEnCurso";

    /// <summary>Secuencia que alimenta el número de viaje. La declara <c>GtDbContext</c>.</summary>
    public const string Secuencia = "NumeroDeViaje";

    public void Configure(EntityTypeBuilder<Viaje> tabla)
    {
        tabla.ToTable(
            "Viajes",
            // El cero es válido y el negativo no (FR-013). Va como CHECK y no sólo como validación de
            // la capa de aplicación porque es una garantía sobre el dato, no sobre el formulario.
            constructor => constructor.HasCheckConstraint("CK_Viajes_Importe", "[Importe] >= 0"));

        tabla.HasKey(viaje => viaje.Id);

        // El valor lo pone el DEFAULT de la columna y EF lo recupera por OUTPUT después del INSERT.
        // `ValueGeneratedOnAdd` es la mitad que hace que EF **omita** la columna en el INSERT; sin
        // ella mandaría el 0 del constructor y el default no se aplicaría nunca (tasks §trampa 2).
        tabla.Property(viaje => viaje.Numero)
            .HasDefaultValueSql($"NEXT VALUE FOR dbo.{Secuencia}")
            .ValueGeneratedOnAdd()
            .IsRequired();

        tabla.Property(viaje => viaje.ClienteId).IsRequired();
        tabla.Property(viaje => viaje.Fecha).IsRequired();
        tabla.Property(viaje => viaje.Origen).HasMaxLength(100).IsRequired();
        tabla.Property(viaje => viaje.Destino).HasMaxLength(100).IsRequired();
        tabla.Property(viaje => viaje.NumeroRemito).HasMaxLength(50);
        tabla.Property(viaje => viaje.DetalleCarga).HasMaxLength(500);
        tabla.Property(viaje => viaje.MotivoAnulacion).HasMaxLength(500);

        // `decimal`, nunca punto flotante: un total que alguien va a comparar contra una planilla no
        // puede acumular error de representación (research §11).
        tabla.Property(viaje => viaje.Importe).HasColumnType("decimal(18,2)").IsRequired();

        tabla.Property(viaje => viaje.Estado).HasConversion<byte>().IsRequired();

        // ── Los tres índices que son la garantía real, no una optimización ──────────────────────
        // La consulta previa de cada caso de uso da el mensaje bueno; el índice cierra la carrera
        // entre dos operadores simultáneos. Es lo que hace que el 0% de SC-005 valga también cuando
        // los dos actúan en el mismo milisegundo (research §2).
        //
        // Los literales `1` y `3` son los valores de EstadoViaje. Reordenar ese enum no falla al
        // compilar y dejaría estos tres filtros protegiendo el estado equivocado: lo cubre
        // IndicesFiltradosTests.

        tabla.HasIndex(viaje => viaje.Numero)
            .IsUnique()
            .HasDatabaseName(IndiceNumero);

        // FR-014: único entre los NO anulados. Un viaje sin remito no ocupa nada, y el remito de un
        // viaje anulado vuelve a estar libre.
        tabla.HasIndex(viaje => viaje.NumeroRemito)
            .IsUnique()
            .HasFilter("[NumeroRemito] IS NOT NULL AND [Estado] <> 3")
            .HasDatabaseName(IndiceRemito);

        // FR-026: un chofer, un solo viaje `en curso` a la vez. Dos viajes `pendiente` con el mismo
        // chofer y la misma fecha se aceptan: un pendiente no ocupa a nadie (FR-027).
        tabla.HasIndex(viaje => viaje.ChoferId)
            .IsUnique()
            .HasFilter("[ChoferId] IS NOT NULL AND [Estado] = 1")
            .HasDatabaseName(IndiceChoferEnCurso);

        tabla.HasIndex(viaje => viaje.VehiculoId)
            .IsUnique()
            .HasFilter("[VehiculoId] IS NOT NULL AND [Estado] = 1")
            .HasDatabaseName(IndiceVehiculoEnCurso);

        // ── Índices de consulta ─────────────────────────────────────────────────────────────────
        // El del listado sigue su orden exacto: fecha descendente y, a igual fecha, número
        // descendente (FR-043).
        tabla.HasIndex(viaje => new { viaje.Fecha, viaje.Numero })
            .IsDescending(true, true)
            .HasDatabaseName("IX_Viajes_Fecha_Numero");

        tabla.HasIndex(viaje => viaje.ClienteId);
        tabla.HasIndex(viaje => viaje.TransportistaId);
        tabla.HasIndex(viaje => viaje.Estado);

        // Módulo 6, FR-053. **No único, y no es un descuido**: una factura tiene muchos viajes. La
        // exclusividad —que un viaje no entre en dos facturas— ya la garantiza la forma del dato: una
        // columna escalar no puede apuntar a dos filas, así que no hay nada que un índice único
        // agregue. Lo que falta cerrar es la carrera entre dos operadores simultáneos, y eso lo cierra
        // el `UPDATE` condicional de `RepositorioFacturas` (Módulo 6, research §4).
        //
        // Los tres índices filtrados de arriba **no se tocan**: el enum sumó `Facturado = 4` al final,
        // así que el `1` y el `3` siguen apuntando a lo mismo. El de remito pasa a cubrir también a
        // los facturados, que es lo correcto: un viaje facturado no libera su remito.
        tabla.HasIndex(viaje => viaje.FacturaId).HasDatabaseName("IX_Viajes_FacturaId");

        // ── Las cuatro claves foráneas, todas en Restrict ───────────────────────────────────────
        // Nada de lo que este módulo referencia se borra físicamente: los cuatro padrones usan baja
        // lógica, así que el borrado en cascada no tiene a quién servir.
        tabla.HasOne(viaje => viaje.Cliente)
            .WithMany(cliente => cliente.Viajes)
            .HasForeignKey(viaje => viaje.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        // Sin navegación inversa: los Módulos 3 y 4 no se modifican, ni siquiera con una colección
        // (spec §Assumptions). El viaje conoce al chofer; el chofer no sabe de viajes.
        tabla.HasOne(viaje => viaje.Chofer)
            .WithMany()
            .HasForeignKey(viaje => viaje.ChoferId)
            .OnDelete(DeleteBehavior.Restrict);

        tabla.HasOne(viaje => viaje.Vehiculo)
            .WithMany()
            .HasForeignKey(viaje => viaje.VehiculoId)
            .OnDelete(DeleteBehavior.Restrict);

        tabla.HasOne(viaje => viaje.Transportista)
            .WithMany()
            .HasForeignKey(viaje => viaje.TransportistaId)
            .OnDelete(DeleteBehavior.Restrict);

        // Módulo 6. En `Restrict` como las otras cuatro: nada borra facturas, y una factura sin sus
        // viajes no diría lo mismo que su documento.
        tabla.HasOne(viaje => viaje.Factura)
            .WithMany(factura => factura.Viajes)
            .HasForeignKey(viaje => viaje.FacturaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
