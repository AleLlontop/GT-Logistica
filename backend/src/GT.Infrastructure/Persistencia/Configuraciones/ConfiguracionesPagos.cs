using GT.Domain.Pagos;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GT.Infrastructure.Persistencia.Configuraciones;

internal static class RestriccionesTextoPago
{
    public const string Binaria = "Latin1_General_100_BIN2";
    // Mismo conjunto Unicode de String.Trim() de .NET; no sólo el espacio ASCII de TRIM por defecto.
    private static readonly string Blancos = string.Join("+", Enumerable.Range(0, char.MaxValue + 1)
        .Where(i => char.IsWhiteSpace((char)i)).Select(i => $"NCHAR({i})"));
    public static string Normalizado(string columna) =>
        $"DATALENGTH([{columna}]) > 0 AND DATALENGTH([{columna}]) <= 400 AND " +
        $"DATALENGTH([{columna}]) = DATALENGTH(TRIM({Blancos} FROM [{columna}] COLLATE {Binaria}))";
}

public class BancoConfiguracion : IEntityTypeConfiguration<Banco>
{
    public void Configure(EntityTypeBuilder<Banco> t)
    {
        t.ToTable("Bancos", c => c.HasCheckConstraint("CK_Bancos_Nombre", RestriccionesTextoPago.Normalizado("Nombre")));
        t.HasKey(x => x.Id);
        // Nombre hereda la collation textual del padrón; no aplicar BIN2 al índice de nombres.
        t.Property(x => x.Nombre).HasMaxLength(200).IsRequired();
        t.Property(x => x.Activo).HasDefaultValue(true).HasSentinel(true);
        t.Property(x => x.Version).HasDefaultValue(0);
        t.HasIndex(x => x.Nombre).IsUnique().HasDatabaseName("IX_Bancos_Nombre");
    }
}

public class OrdenPagoProveedorConfiguracion : IEntityTypeConfiguration<OrdenPagoProveedor>
{
    public const string Secuencia = "NumeroDeOrdenPagoProveedor";
    public void Configure(EntityTypeBuilder<OrdenPagoProveedor> t)
    {
        t.ToTable("OrdenesPagoProveedor", c =>
        {
            c.HasCheckConstraint("CK_OPP_Importe", "[ImporteTotal] > 0");
            c.HasCheckConstraint("CK_OPP_Estado", "([Estado]=0 AND [MotivoAnulacion] IS NULL AND [AnuladaPorId] IS NULL AND [AnuladaEn] IS NULL) OR " +
                "([Estado]=1 AND [MotivoAnulacion] IS NOT NULL AND LEN(LTRIM(RTRIM([MotivoAnulacion])))>0 AND [AnuladaPorId] IS NOT NULL AND [AnuladaEn] IS NOT NULL)");
        });
        t.HasKey(x => x.Id);
        t.Property(x => x.Numero).HasDefaultValueSql($"NEXT VALUE FOR dbo.{Secuencia}").ValueGeneratedOnAdd();
        t.HasIndex(x => x.Numero).IsUnique();
        t.Property(x => x.Estado).HasConversion<byte>();
        t.Property(x => x.TipoProveedorAlRegistrar).HasConversion<int>();
        t.Property(x => x.ProveedorRazonSocial).HasMaxLength(200).IsRequired();
        t.Property(x => x.ProveedorCuit).HasMaxLength(11).IsUnicode(false).IsRequired();
        t.Property(x => x.ImporteTotal).HasPrecision(18, 2);
        t.HasIndex(x => new { x.Fecha, x.Id }).IsDescending(true, true);
        t.HasOne(x => x.Proveedor).WithMany().HasForeignKey(x => x.ProveedorId).OnDelete(DeleteBehavior.Restrict);
        t.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        t.HasOne(x => x.AnuladaPor).WithMany().HasForeignKey(x => x.AnuladaPorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class OrdenPagoProveedorFacturaConfiguracion : IEntityTypeConfiguration<OrdenPagoProveedorFactura>
{
    public void Configure(EntityTypeBuilder<OrdenPagoProveedorFactura> t)
    {
        t.ToTable("OrdenPagoProveedorFacturas", c => c.HasCheckConstraint("CK_OPPFactura_Importe", "[ImporteTotalAlPagar]>0"));
        t.HasKey(x => new { x.OrdenPagoProveedorId, x.FacturaProveedorId });
        t.Property(x => x.ImporteTotalAlPagar).HasPrecision(18, 2);
        t.Property(x => x.Vigente).HasDefaultValue(true).HasSentinel(true);
        t.HasIndex(x => x.FacturaProveedorId).IsUnique().HasFilter("[Vigente]=1").HasDatabaseName("IX_OPPFactura_Vigente");
        t.HasOne(x => x.OrdenPagoProveedor).WithMany(x => x.Facturas).HasForeignKey(x => x.OrdenPagoProveedorId).OnDelete(DeleteBehavior.Restrict);
        t.HasOne(x => x.FacturaProveedor).WithMany().HasForeignKey(x => x.FacturaProveedorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class MedioPagoProveedorConfiguracion : IEntityTypeConfiguration<MedioPagoProveedor>
{
    public void Configure(EntityTypeBuilder<MedioPagoProveedor> t)
    {
        t.ToTable("MediosPagoProveedor", c =>
        {
            c.HasCheckConstraint("CK_MedioPago_Importe", "[Importe]>0");
            c.HasCheckConstraint("CK_MedioPago_Tipo", "[Tipo] IN (0,1,2)");
            c.HasCheckConstraint("CK_MedioPago_Campos", "(" +
                "[Tipo]=0 AND [BancoId] IS NOT NULL AND [NumeroCheque] IS NOT NULL AND [FechaVencimiento] IS NOT NULL AND [EstadoCheque] IS NOT NULL AND [EstadoCheque] IN(0,1,2,3,4,5) AND [FechaVencimiento]>=[Fecha] AND " +
                "[CbuOrigen] IS NULL AND [CbuDestino] IS NULL AND [NumeroComprobante] IS NULL AND [Observaciones] IS NULL) OR (" +
                "[Tipo]=1 AND [CbuOrigen] IS NOT NULL AND [CbuDestino] IS NOT NULL AND [NumeroComprobante] IS NOT NULL AND LEN(LTRIM(RTRIM([NumeroComprobante])))>0 AND " +
                "[BancoId] IS NULL AND [NumeroCheque] IS NULL AND [FechaVencimiento] IS NULL AND [EstadoCheque] IS NULL AND [DetalleCheque] IS NULL) OR (" +
                "[Tipo]=2 AND [BancoId] IS NULL AND [NumeroCheque] IS NULL AND [FechaVencimiento] IS NULL AND [EstadoCheque] IS NULL AND [DetalleCheque] IS NULL AND " +
                "[CbuOrigen] IS NULL AND [CbuDestino] IS NULL AND [NumeroComprobante] IS NULL AND [Observaciones] IS NULL)");
            c.HasCheckConstraint("CK_Cheque_Numero", $"[Tipo]<>0 OR ([NumeroCheque] IS NOT NULL AND {RestriccionesTextoPago.Normalizado("NumeroCheque")})");
            c.HasCheckConstraint("CK_Transferencia_Cbu", "[Tipo]<>1 OR ([CbuOrigen] IS NOT NULL AND [CbuDestino] IS NOT NULL AND DATALENGTH([CbuOrigen])=22 AND DATALENGTH([CbuDestino])=22 AND " +
                "[CbuOrigen] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9]%' AND [CbuDestino] COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9]%')");
        });
        t.HasKey(x => x.Id);
        t.Property(x => x.Tipo).HasConversion<byte>();
        t.HasDiscriminator(x => x.Tipo).HasValue<Cheque>(TipoMedioPagoProveedor.Cheque)
            .HasValue<Transferencia>(TipoMedioPagoProveedor.Transferencia).HasValue<Efectivo>(TipoMedioPagoProveedor.Efectivo);
        t.HasAlternateKey(x => new { x.Id, x.Tipo });
        t.Property(x => x.Importe).HasPrecision(18, 2);
        t.HasIndex(x => new { x.OrdenPagoProveedorId, x.RenglonId }).IsUnique();
        t.HasOne(x => x.OrdenPagoProveedor).WithMany(x => x.Medios).HasForeignKey(x => x.OrdenPagoProveedorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ChequeConfiguracion : IEntityTypeConfiguration<Cheque>
{
    public void Configure(EntityTypeBuilder<Cheque> t)
    {
        t.Property(x => x.NumeroCheque).HasMaxLength(200).UseCollation(RestriccionesTextoPago.Binaria).IsRequired();
        t.Property(x => x.EstadoCheque).HasConversion<byte>();
        t.HasOne(x => x.Banco).WithMany().HasForeignKey(x => x.BancoId).OnDelete(DeleteBehavior.Restrict);
        t.HasIndex(x => new { x.BancoId, x.NumeroCheque }).IsUnique().HasFilter("[Tipo]=0").HasDatabaseName("IX_Cheques_Banco_Numero");
    }
}

public class TransferenciaConfiguracion : IEntityTypeConfiguration<Transferencia>
{
    public void Configure(EntityTypeBuilder<Transferencia> t)
    {
        t.Property(x => x.CbuOrigen).HasMaxLength(22).IsUnicode(false).IsRequired();
        t.Property(x => x.CbuDestino).HasMaxLength(22).IsUnicode(false).IsRequired();
        t.Property(x => x.NumeroComprobante).IsRequired();
    }
}

public class CambioEstadoChequeConfiguracion : IEntityTypeConfiguration<CambioEstadoCheque>
{
    public void Configure(EntityTypeBuilder<CambioEstadoCheque> t)
    {
        t.ToTable("HistorialEstadosCheque", c =>
        {
            c.HasCheckConstraint("CK_HistorialCheque_Tipo", "[Tipo]=0");
            c.HasCheckConstraint("CK_HistorialCheque_Arista", "([EstadoAnterior]=0 AND [EstadoNuevo] IN(1,5)) OR ([EstadoAnterior]=1 AND [EstadoNuevo] IN(2,5)) OR ([EstadoAnterior]=2 AND [EstadoNuevo] IN(3,4))");
        });
        t.HasKey(x => x.Id);
        t.Property(x => x.Tipo).HasConversion<byte>();
        t.Property(x => x.EstadoAnterior).HasConversion<byte>();
        t.Property(x => x.EstadoNuevo).HasConversion<byte>();
        t.HasOne(x => x.Cheque).WithMany().HasForeignKey(x => new { x.ChequeId, x.Tipo }).HasPrincipalKey(x => new { x.Id, x.Tipo }).OnDelete(DeleteBehavior.Restrict);
        t.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        t.HasIndex(x => new { x.ChequeId, x.Id });
    }
}

public class ResultadoOperacionPagoConfiguracion : IEntityTypeConfiguration<ResultadoOperacionPago>
{
    public void Configure(EntityTypeBuilder<ResultadoOperacionPago> t)
    {
        t.ToTable("ResultadosOperacionPago", c =>
        {
            c.HasCheckConstraint("CK_ResultadoPago_Json", "ISJSON([ContenidoCanonico])=1 AND ISJSON([ResultadoJson])=1");
            c.HasCheckConstraint("CK_ResultadoPago_Operacion", "[Operacion] IN(0,1) AND [VersionCanonica]>0 AND [EstadoHttp] IN(200,201)");
        });
        t.HasKey(x => x.Id);
        t.Property(x => x.Operacion).HasConversion<byte>();
        t.Property(x => x.Hash).HasColumnType("binary(32)").IsRequired();
        t.Property(x => x.ContenidoCanonico).IsRequired();
        t.Property(x => x.ResultadoJson).IsRequired();
        t.HasIndex(x => new { x.Operacion, x.Clave }).IsUnique().HasDatabaseName("IX_ResultadoPago_Operacion_Clave");
        t.HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
        t.HasOne(x => x.OrdenPagoProveedor).WithMany().HasForeignKey(x => x.OrdenPagoProveedorId).OnDelete(DeleteBehavior.Restrict);
    }
}
