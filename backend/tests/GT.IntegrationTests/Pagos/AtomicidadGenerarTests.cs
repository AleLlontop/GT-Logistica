using System.Data.Common;
using GT.Application.Pagos;
using GT.Domain.Liquidaciones;
using GT.Domain.Pagos;
using GT.Domain.Proveedores;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Caja;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Proveedores;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

public class AtomicidadGenerarTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 18);
    private static readonly DateTime Ahora = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData("OrdenesPagoProveedor")]
    [InlineData("OrdenPagoProveedorFacturas")]
    [InlineData("MediosPagoProveedor")]
    [InlineData("FacturasProveedor")]
    [InlineData("OrdenesDePago")]
    [InlineData("CambiosDeLiquidacion")]
    [InlineData("MovimientosDeCaja")]
    [InlineData("ResultadosOperacionPago")]
    public async Task FalloTrasEscrituraReal_RevierteGrafoCompleto(string tabla)
    {
        var app = new AplicacionConFallo(tabla);
        try
        {
            await app.InitializeAsync();
            var (entrada, usuario) = await Preparar(app, tabla is "OrdenesDePago" or "CambiosDeLiquidacion", tabla == "MovimientosDeCaja");
            var antes = await Foto(app);
            app.Fallo.Activo = true;
            using (var scope = app.Services.CreateScope())
            {
                var error = await Assert.ThrowsAnyAsync<Exception>(() => scope.ServiceProvider.GetRequiredService<IRepositorioPagos>()
                    .GenerarAsync(entrada, usuario, true, Hoy, Ahora));
                Assert.IsType<IOException>(error.GetBaseException());
            }
            Assert.True(app.Fallo.Disparado);
            Assert.Equal(antes, await Foto(app));
            await app.EnLaBaseAsync(async db =>
            {
                Assert.Empty(await db.ResultadosOperacionPago.Where(x => x.Clave == entrada.ClaveOperacion).ToListAsync());
                Assert.Equal(EstadoFacturaProveedor.Pendiente, (await db.FacturasProveedor.SingleAsync(x => x.Id == entrada.FacturaIds[0])).Estado);
                Assert.All(await db.Liquidaciones.ToListAsync(), l => { Assert.Equal(0m, l.ImportePagado); Assert.Equal(EstadoLiquidacion.Pendiente, l.Estado); });
            });
        }
        finally { await app.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimeoutODeadlockSqlReal_NoDevuelveExitoNiResultadoParcial(bool deadlock)
    {
        var app = new AplicacionConFallo("ResultadosOperacionPago");
        try
        {
            await app.InitializeAsync();
            var (entrada, usuario) = await Preparar(app, false, false);
            var bancoBloqueado = await app.ConAlcanceAsync(async db =>
            {
                var banco = new Banco { Nombre = "Banco reservado para fallo SQL" }; db.Add(banco); await db.SaveChangesAsync(); return banco.Id;
            });
            var antes = await Foto(app);
            using var scopeBloqueante = app.Services.CreateScope();
            var dbBloqueante = scopeBloqueante.ServiceProvider.GetRequiredService<GtDbContext>();
            await using var tx = await dbBloqueante.Database.BeginTransactionAsync();
            await dbBloqueante.Bancos.Where(x => x.Id == bancoBloqueado).ExecuteUpdateAsync(u => u.SetProperty(x => x.Version, x => x.Version));
            app.Fallo.BancoBloqueado = bancoBloqueado;
            app.Fallo.Deadlock = deadlock;
            app.Fallo.Activo = true;
            async Task SegundaArista()
            {
                await app.Fallo.EnPunto.Task.WaitAsync(TimeSpan.FromSeconds(30));
                var bancoOriginal = ((ChequeEntrada)entrada.Medios[0]).BancoId;
                await dbBloqueante.Bancos.Where(x => x.Id == bancoOriginal).ExecuteUpdateAsync(u => u.SetProperty(x => x.Version, x => x.Version));
                await tx.CommitAsync();
            }
            var arista = deadlock ? SegundaArista() : Task.CompletedTask;
            using (var scope = app.Services.CreateScope())
            {
                var error = await Assert.ThrowsAsync<ReglaPagoException>(() => scope.ServiceProvider.GetRequiredService<IRepositorioPagos>()
                    .GenerarAsync(entrada, usuario, true, Hoy, Ahora));
                Assert.Equal(503, error.EstadoHttp);
                Assert.Equal("operacion_transitoria", error.Codigo);
            }
            await arista.WaitAsync(TimeSpan.FromSeconds(30));
            if (!deadlock) await tx.RollbackAsync();
            Assert.Equal(deadlock ? 1205 : -2, app.Fallo.NumeroSql);
            Assert.Equal(antes, await Foto(app));
        }
        finally { await app.DisposeAsync(); }
    }

    private static Task<int[]> Foto(AplicacionDePrueba app) => app.ConAlcanceAsync(async db => new[]
    {
        await db.OrdenesPagoProveedor.CountAsync(), await db.OrdenPagoProveedorFacturas.CountAsync(), await db.MediosPagoProveedor.CountAsync(),
        await db.OrdenesDePago.CountAsync(), await db.CambiosDeLiquidacion.CountAsync(), await db.MovimientosDeCaja.CountAsync(),
        await db.ResultadosOperacionPago.CountAsync(), await db.HistorialEstadosCheque.CountAsync(),
    });

    private static async Task<(GenerarOrdenEntrada, int)> Preparar(AplicacionDePrueba app, bool fletero, bool efectivo)
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var factura = s.Factura;
        if (fletero) factura = await app.ConAlcanceAsync(async db =>
        {
            var t = EscenarioProveedores.Externo("20" + Random.Shared.NextInt64(100000000, 999999999));
            var p = EscenarioProveedores.Fletero(t);
            var l = EscenarioProveedores.Pendiente(t, 121m);
            var f = new FacturaProveedor { Proveedor = p, TipoProveedorAlRegistrar = TipoProveedor.Fletero,
                ProveedorRazonSocial = t.Nombre, ProveedorCuit = t.Cuit, Numero = Guid.NewGuid().ToString("N"), Fecha = Hoy,
                Detalle = "Flete", Neto = 100m, Iva = 21m, ImporteTotal = 121m, Cae = "1", FechaVencimientoCae = Hoy,
                Liquidacion = l, ImporteLiquidacionAlRegistrar = 121m };
            db.Add(f); db.CambiosDeLiquidacion.Add(new() { LiquidacionId = 0, Liquidacion = l,
                Operacion = OperacionDeLiquidacion.Generacion, UsuarioId = s.UsuarioId, OcurridoEn = Ahora.AddDays(-1) });
            await db.SaveChangesAsync(); return f;
        });
        if (efectivo) await app.CrearCajaAsync(s.UsuarioId, saldoInicial: 121m);
        return (new(Guid.NewGuid(), factura.ProveedorId, Hoy, null, [factura.Id],
            efectivo ? [new EfectivoEntrada(Guid.NewGuid(), 121m)]
                : [new ChequeEntrada(Guid.NewGuid(), 121m, s.Banco.Id, "000-123", Hoy, Hoy.AddDays(1), null)], 121m, true), s.UsuarioId);
    }

    internal sealed class AplicacionConFallo(string tabla) : AplicacionDePrueba
    {
        public FalloDespuesDeEscribir Fallo { get; } = new(tabla);
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(s => s.AddDbContext<GtDbContext>(o => o.UseSqlServer(sql => sql.MaxBatchSize(1)).AddInterceptors(Fallo)));
        }
    }
    internal sealed class FalloDespuesDeEscribir(string tabla) : DbCommandInterceptor
    {
        public bool Activo;
        public bool Disparado;
        public bool Deadlock;
        public int? BancoBloqueado;
        public int? NumeroSql;
        private int actualizacionesFactura;
        public TaskCompletionSource EnPunto { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private bool Coincide(DbCommand command)
        {
            if (!Activo || Disparado || !command.CommandText.Contains($"[{tabla}]", StringComparison.Ordinal)) return false;
            if (tabla == "FacturasProveedor") return command.CommandText.StartsWith("UPDATE", StringComparison.Ordinal)
                && ++actualizacionesFactura == 2;
            return command.CommandText.Contains($"INSERT INTO [{tabla}]", StringComparison.Ordinal)
                || command.CommandText.Contains($"MERGE [{tabla}]", StringComparison.Ordinal);
        }
        private async Task Fallar(DbCommand command, CommandExecutedEventData data, DbDataReader? lector, CancellationToken ct)
        {
            Disparado = true;
            // El interceptor entrega la excepción en lugar del lector; le corresponde cerrar ese recurso.
            if (lector is not null) await lector.DisposeAsync();
            if (!BancoBloqueado.HasValue) throw new IOException("Fallo inyectado después de escritura SQL real: " + tabla);
            EnPunto.TrySetResult();
            await using var bloqueo = command.Connection!.CreateCommand();
            bloqueo.Transaction = data.Context!.Database.CurrentTransaction!.GetDbTransaction();
            bloqueo.CommandText = (Deadlock ? "SET DEADLOCK_PRIORITY LOW; " : "") + "UPDATE Bancos SET Version=Version WHERE Id=@id";
            var parametro = bloqueo.CreateParameter(); parametro.ParameterName = "@id"; parametro.Value = BancoBloqueado.Value; bloqueo.Parameters.Add(parametro);
            bloqueo.CommandTimeout = Deadlock ? 30 : 1;
            try { await bloqueo.ExecuteNonQueryAsync(ct); }
            catch (SqlException e) { NumeroSql = e.Number; throw; }
            throw new InvalidOperationException("El fallo SQL esperado no ocurrió.");
        }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (Coincide(command)) await Fallar(command, eventData, result, cancellationToken);
            return result;
        }
        public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            int result, CancellationToken cancellationToken = default)
        {
            if (Coincide(command)) await Fallar(command, eventData, null, cancellationToken);
            return result;
        }
    }
}
