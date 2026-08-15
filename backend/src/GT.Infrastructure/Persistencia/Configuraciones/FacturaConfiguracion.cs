using GT.Domain.Facturacion;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

public class FacturaConfiguracion : IEntityTypeConfiguration<FacturaCliente>
{
    /// <summary>
    /// Nombres de los dos índices únicos. <c>RepositorioFacturas</c> los lee del mensaje de la
    /// excepción de SQL Server para saber **cuál** se violó y traducirlo al rechazo que corresponde:
    /// sin esa distinción, una carrera por el número y una por la refacturación llegarían arriba como
    /// el mismo error (convención [003]).
    /// </summary>
    public const string IndiceNumero = "IX_Facturas_Numero";

    public const string IndiceFacturaReemplazada = "IX_Facturas_FacturaReemplazada";

    public const string CheckTotal = "CK_Facturas_Total";

    public const string CheckPeriodoMes = "CK_Facturas_PeriodoMes";

    public void Configure(EntityTypeBuilder<FacturaCliente> tabla)
    {
        tabla.ToTable("Facturas", constructor =>
        {
            // El total no es un dato aparte de los otros dos: es su suma, y la base lo verifica. Que
            // el cálculo viva en el dominio no quita que una fila con total inconsistente no deba
            // poder existir (FR-022, FR-023).
            constructor.HasCheckConstraint(CheckTotal, "[Total] = [Neto] + [Iva]");

            // El año **no** lleva CHECK a propósito: la lista de años válidos se amplía con el tiempo
            // y una restricción de base obligaría a una migración cada vez (spec §Assumptions). El mes
            // sí, porque 1–12 no cambia nunca.
            constructor.HasCheckConstraint(CheckPeriodoMes, "[PeriodoMes] BETWEEN 1 AND 12");
        });

        tabla.HasKey(factura => factura.Id);

        // ── Identificación y clasificación ──────────────────────────────────────────────────────
        tabla.Property(factura => factura.NumeroComprobante).HasMaxLength(13).IsRequired();
        tabla.Property(factura => factura.Fecha).IsRequired();
        tabla.Property(factura => factura.TipoComprobante).HasConversion<byte>().IsRequired();
        tabla.Property(factura => factura.TipoFacturacion).HasConversion<byte>().IsRequired();
        tabla.Property(factura => factura.CondicionDeVenta).HasConversion<byte>().IsRequired();
        tabla.Property(factura => factura.PeriodoMes).IsRequired();
        tabla.Property(factura => factura.PeriodoAnio).IsRequired();
        tabla.Property(factura => factura.Detalle).HasMaxLength(500);

        // ── Cliente: referencia y copia congelada (FR-034a) ─────────────────────────────────────
        tabla.Property(factura => factura.ClienteId).IsRequired();
        tabla.Property(factura => factura.ClienteRazonSocial).HasMaxLength(100).IsRequired();
        tabla.Property(factura => factura.ClienteCuit).HasMaxLength(11).IsRequired();

        // No admite nulo, y es lo que vuelve obligatorio el domicilio para facturar (FR-011a): un
        // cliente sin domicilio no puede llegar acá aunque alguien saltee la validación.
        tabla.Property(factura => factura.ClienteDomicilio).HasMaxLength(200).IsRequired();

        // ── Emisor: diez columnas, sólo copia (FR-034) ──────────────────────────────────────────
        // Los cuatro primeros no admiten nulo —son los obligatorios de FR-002—; los otros seis sí,
        // porque son opcionales en la configuración. No hay `EmpresaEmisoraId`: la factura no
        // referencia la configuración, la copia.
        tabla.Property(factura => factura.EmisorRazonSocial).HasMaxLength(200).IsRequired();
        tabla.Property(factura => factura.EmisorCuit).HasMaxLength(11).IsRequired();
        tabla.Property(factura => factura.EmisorDomicilio).HasMaxLength(200).IsRequired();
        tabla.Property(factura => factura.EmisorCondicionIva).HasMaxLength(100).IsRequired();
        tabla.Property(factura => factura.EmisorIngresosBrutos).HasMaxLength(50);
        tabla.Property(factura => factura.EmisorPuntoDeVenta).HasMaxLength(4);
        tabla.Property(factura => factura.EmisorCbu).HasMaxLength(22);
        tabla.Property(factura => factura.EmisorTelefono).HasMaxLength(50);
        tabla.Property(factura => factura.EmisorEmail).HasMaxLength(254);

        // ── Importes ────────────────────────────────────────────────────────────────────────────
        // `decimal`, nunca punto flotante: un total que alguien va a comparar contra una planilla no
        // puede acumular error de representación (convención [005]).
        tabla.Property(factura => factura.Neto).HasColumnType("decimal(18,2)").IsRequired();
        tabla.Property(factura => factura.Iva).HasColumnType("decimal(18,2)").IsRequired();
        tabla.Property(factura => factura.Total).HasColumnType("decimal(18,2)").IsRequired();

        // Derivada del tipo de comprobante, no almacenada (research §5). Sin esto EF intentaría
        // mapearla a una columna que el modelo deliberadamente no tiene.
        tabla.Ignore(factura => factura.Alicuota);

        // ── CAE y vencimientos ──────────────────────────────────────────────────────────────────
        tabla.Property(factura => factura.Cae).HasMaxLength(20).IsRequired();
        tabla.Property(factura => factura.CaeVencimiento).IsRequired();
        tabla.Property(factura => factura.VencimientoPago).IsRequired();

        // ── Estado, cobro y anulación ───────────────────────────────────────────────────────────
        tabla.Property(factura => factura.Estado).HasConversion<byte>().IsRequired();
        tabla.Property(factura => factura.MotivoAnulacion).HasMaxLength(500);

        // ── Documento ───────────────────────────────────────────────────────────────────────────
        // No admite nulo: toda factura emitida tiene su documento, porque se genera en la misma
        // operación que la crea (FR-031, SC-007a).
        tabla.Property(factura => factura.DocumentoRuta).HasMaxLength(260).IsRequired();

        // ── Los dos índices únicos que son la garantía real, no una optimización ────────────────
        // La consulta previa de cada caso de uso da el mensaje bueno; el índice cierra la carrera
        // entre dos operadores simultáneos (SC-004, research §4).
        //
        // ⚠ El `2` es `EstadoFactura.Anulada`, escrito a mano. Reordenar ese enum no falla al
        // compilar y dejaría el índice protegiendo el estado equivocado: el número pasaría a ser
        // único entre las anuladas y dos vigentes podrían compartirlo. Lo cubre
        // `IndicesDeFacturaTests`, que inserta una fila en cada estado y verifica dónde acepta y
        // dónde rechaza.

        // FR-027: único entre las **no anuladas**. Anular libera el número.
        tabla.HasIndex(factura => factura.NumeroComprobante)
            .IsUnique()
            .HasFilter("[Estado] <> 2")
            .HasDatabaseName(IndiceNumero);

        // FR-049a: a una factura anulada la reemplaza **a lo sumo una** Refacturación.
        tabla.HasIndex(factura => factura.FacturaReemplazadaId)
            .IsUnique()
            .HasFilter("[FacturaReemplazadaId] IS NOT NULL")
            .HasDatabaseName(IndiceFacturaReemplazada);

        // ── Índices de consulta ─────────────────────────────────────────────────────────────────
        // El del listado sigue su orden exacto: fecha descendente y, a igual fecha, número
        // descendente (FR-059).
        tabla.HasIndex(factura => new { factura.Fecha, factura.NumeroComprobante })
            .IsDescending(true, true)
            .HasDatabaseName("IX_Facturas_Fecha_Numero");

        tabla.HasIndex(factura => factura.ClienteId).HasDatabaseName("IX_Facturas_ClienteId");

        // FR-041, FR-063: el filtro por estado derivado y el panel de vencimientos.
        tabla.HasIndex(factura => new { factura.Estado, factura.VencimientoPago })
            .HasDatabaseName("IX_Facturas_Estado_VencimientoPago");

        // ── Claves foráneas, las dos en Restrict ────────────────────────────────────────────────
        // Nada de lo que este módulo referencia se borra físicamente, y una factura menos que nunca:
        // borrar en cascada no tiene a quién servir.
        tabla.HasOne(factura => factura.Cliente)
            .WithMany()
            .HasForeignKey(factura => factura.ClienteId)
            .OnDelete(DeleteBehavior.Restrict);

        // Auto-referencia. Sin navegación inversa a propósito: la otra dirección se resuelve con una
        // consulta y no con una columna espejo que habría que mantener sincronizada (FR-050).
        tabla.HasOne(factura => factura.FacturaReemplazada)
            .WithMany()
            .HasForeignKey(factura => factura.FacturaReemplazadaId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
