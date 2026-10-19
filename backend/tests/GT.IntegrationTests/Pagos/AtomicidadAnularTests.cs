using System.Data.Common;
using System.Text.Json;
using GT.Application.Pagos;
using GT.Domain.Pagos;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

public class AtomicidadAnularTests
{
    [Theory]
    [InlineData("OrdenPagoProveedorFacturas", "cheque")]
    [InlineData("FacturasProveedor", "cheque")]
    [InlineData("OrdenesDePago", "fletero")]
    [InlineData("Liquidaciones", "fletero")]
    [InlineData("CambiosDeLiquidacion", "fletero")]
    [InlineData("MediosPagoProveedor", "cheque")]
    [InlineData("HistorialEstadosCheque", "cheque")]
    [InlineData("MovimientosDeCaja", "efectivo")]
    [InlineData("OrdenesPagoProveedor", "cheque")]
    [InlineData("ResultadosOperacionPago", "cheque")]
    public async Task FalloDespuesDeMutacionSql_RevierteGrafoEntero(string tabla, string tipo)
    {
        var app = new AplicacionFalloAnular(tabla);
        try
        {
            await app.InitializeAsync(); var s = await EscenarioAnulacion.CrearAsync(app, tipo);
            var antes = await Foto(app, s); app.Fallo.Activo = true;
            using (var scope = app.Services.CreateScope())
            {
                var error = await Assert.ThrowsAnyAsync<Exception>(() => scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().AnularAsync(s.OrdenId,
                    new(Guid.NewGuid(), "Corrección", true), s.UsuarioId, true, EscenarioAnulacion.Hoy, EscenarioAnulacion.Ahora.AddMinutes(1)));
                Assert.IsType<IOException>(error.GetBaseException());
            }
            Assert.True(app.Fallo.Disparado); Assert.Equal(antes, await Foto(app, s));
        }
        finally { await app.DisposeAsync(); }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimeoutODeadlockSqlReal_503SinCompensacionParcial(bool deadlock)
    {
        var app = new AtomicidadGenerarTests.AplicacionConFallo("ResultadosOperacionPago");
        try
        {
            await app.InitializeAsync(); var s = await EscenarioAnulacion.CrearAsync(app, "efectivo");
            var banco = await app.ConAlcanceAsync(async db => { var b = new Banco { Nombre = "Bloqueo SQL " + Guid.NewGuid() }; db.Add(b); await db.SaveChangesAsync(); return b.Id; });
            var antes = await Foto(app, s);
            using var scopeBloqueante = app.Services.CreateScope();
            var dbBloqueante = scopeBloqueante.ServiceProvider.GetRequiredService<GtDbContext>();
            await using var tx = await dbBloqueante.Database.BeginTransactionAsync();
            await dbBloqueante.Bancos.Where(b => b.Id == banco).ExecuteUpdateAsync(u => u.SetProperty(b => b.Version, b => b.Version));
            app.Fallo.BancoBloqueado = banco; app.Fallo.Deadlock = deadlock; app.Fallo.Activo = true;
            async Task SegundaArista()
            {
                await app.Fallo.EnPunto.Task.WaitAsync(TimeSpan.FromSeconds(30));
                await dbBloqueante.OrdenesPagoProveedor.Where(o => o.Id == s.OrdenId).ExecuteUpdateAsync(u => u.SetProperty(o => o.Estado, o => o.Estado));
                await tx.CommitAsync();
            }
            var arista = deadlock ? SegundaArista() : Task.CompletedTask;
            var entrada = new AnularOrdenEntrada(Guid.NewGuid(), "Corrección", true);
            using (var scope = app.Services.CreateScope())
            {
                var error = await Assert.ThrowsAsync<ReglaPagoException>(() => scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().AnularAsync(s.OrdenId,
                    entrada, s.UsuarioId, true, EscenarioAnulacion.Hoy, EscenarioAnulacion.Ahora.AddMinutes(1)));
                Assert.Equal(503, error.EstadoHttp); Assert.Equal("operacion_transitoria", error.Codigo);
            }
            await arista.WaitAsync(TimeSpan.FromSeconds(30)); if (!deadlock) await tx.RollbackAsync();
            Assert.Equal(deadlock ? 1205 : -2, app.Fallo.NumeroSql); Assert.Equal(antes, await Foto(app, s));
            app.Fallo.Activo = false;
            using var reintento = app.Services.CreateScope();
            Assert.Equal(200, (await reintento.ServiceProvider.GetRequiredService<IRepositorioPagos>().AnularAsync(s.OrdenId,
                entrada, s.UsuarioId, true, EscenarioAnulacion.Hoy, EscenarioAnulacion.Ahora.AddMinutes(1))).EstadoHttp);
        }
        finally { await app.DisposeAsync(); }
    }
    private static Task<string> Foto(AplicacionDePrueba app, PagoParaAnular s) => app.ConAlcanceAsync(async db => JsonSerializer.Serialize(new
    {
        Orden = await db.OrdenesPagoProveedor.Where(o => o.Id == s.OrdenId).Select(o => new { o.Estado, o.MotivoAnulacion, o.AnuladaEn, o.AnuladaPorId }).SingleAsync(),
        Facturas = await db.FacturasProveedor.Where(f => s.Facturas.Contains(f.Id)).OrderBy(f => f.Id).Select(f => new { f.Id, f.Estado }).ToListAsync(),
        Vinculos = await db.OrdenPagoProveedorFacturas.Where(v => v.OrdenPagoProveedorId == s.OrdenId).OrderBy(v => v.FacturaProveedorId).Select(v => new { v.FacturaProveedorId, v.Vigente }).ToListAsync(),
        Pagos = await db.OrdenesDePago.Where(p => p.OrdenPagoProveedorId == s.OrdenId).OrderBy(p => p.Id).Select(p => new { p.Id, p.Vigente, p.Importe, p.RevertidaPorId, p.RevertidaEn }).ToListAsync(),
        Liquidaciones = await db.Liquidaciones.Where(l => s.Liquidaciones.Contains(l.Id)).OrderBy(l => l.Id).Select(l => new { l.Id, l.Estado, l.ImporteTotal, l.ImportePagado }).ToListAsync(),
        Cheques = await db.Cheques.Where(c => c.OrdenPagoProveedorId == s.OrdenId).OrderBy(c => c.Id).Select(c => new { c.Id, c.EstadoCheque }).ToListAsync(),
        Historial = await db.HistorialEstadosCheque.CountAsync(), Eventos = await db.CambiosDeLiquidacion.CountAsync(),
        Movimientos = await db.MovimientosDeCaja.OrderBy(m => m.Id).Select(m => new { m.Id, m.CajaId, m.Tipo, m.Importe, m.OrdenPagoProveedorId }).ToListAsync(),
        Resultados = await db.ResultadosOperacionPago.OrderBy(r => r.Id).Select(r => new { r.Id, r.Operacion, r.Clave, r.ResultadoJson }).ToListAsync(),
    }));

    private sealed class AplicacionFalloAnular(string tabla) : AplicacionDePrueba
    {
        public FalloAnular Fallo { get; } = new(tabla);
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(s => s.AddDbContext<GtDbContext>(o => o.UseSqlServer(sql => sql.MaxBatchSize(1)).AddInterceptors(Fallo)));
        }
    }
    private sealed class FalloAnular(string tabla) : DbCommandInterceptor
    {
        public bool Activo; public bool Disparado; private int actualizaciones;
        private bool Coincide(DbCommand cmd)
        {
            if (!Activo || Disparado || !cmd.CommandText.Contains($"[{tabla}]", StringComparison.Ordinal)) return false;
            if (cmd.CommandText.StartsWith("UPDATE", StringComparison.Ordinal)) return ++actualizaciones == (tabla == "OrdenPagoProveedorFacturas" ? 1 : tabla is "Liquidaciones" or "OrdenesDePago" ? 4 : 2);
            return cmd.CommandText.Contains($"INSERT INTO [{tabla}]", StringComparison.Ordinal);
        }
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (Coincide(command)) { Disparado = true; throw new IOException("Fallo tras escritura " + tabla); }
            return ValueTask.FromResult(result);
        }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (Coincide(command)) { Disparado = true; await result.DisposeAsync(); throw new IOException("Fallo tras escritura " + tabla); }
            return result;
        }
    }
}
