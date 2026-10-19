using System.Data.Common;
using GT.Application.Pagos;
using GT.Domain.Pagos;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

public class ChequesTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 18);
    private static readonly DateTime Ahora = new(2026, 10, 18, 15, 0, 0, DateTimeKind.Utc);
    private static async Task<(int Id, int Orden, int Usuario)> Preparar(AplicacionDePrueba app)
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var id = await app.ConAlcanceAsync(async db =>
        {
            var c = new Cheque { OrdenPagoProveedorId = s.Orden.Id, BancoId = s.Banco.Id, NumeroCheque = Guid.NewGuid().ToString(),
                Fecha = Hoy, FechaVencimiento = Hoy.AddDays(1), Importe = 121m, RenglonId = Guid.NewGuid() };
            db.Add(c); await db.SaveChangesAsync(); return c.Id;
        });
        return (id, s.Orden.Id, s.UsuarioId);
    }
    [Fact]
    public async Task DosCambiosSimultaneos_SoloUnoPersisteEstadoEHistorial()
    {
        var app = new ConcurrenciaGenerarTests.AplicacionConBarrera("[OrdenesPagoProveedor]");
        try
        {
            await app.InitializeAsync();
            var s = await Preparar(app);
            app.Barrera.Activa = true;
            async Task<int> Cambiar(EstadoCheque nuevo)
            {
                using var scope = app.Services.CreateScope();
                try
                {
                    await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().CambiarEstadoAsync(s.Id,
                        new(EstadoCheque.Pendiente, nuevo), s.Usuario, Hoy, Ahora);
                    return 200;
                }
                catch (ReglaPagoException e) { return e.EstadoHttp; }
            }
            var resultados = await Task.WhenAll(Cambiar(EstadoCheque.Entregado), Cambiar(EstadoCheque.Anulado)).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(2, app.Barrera.Llegadas);
            Assert.Equal(new[] { 200, 409 }, resultados.Order().ToArray());
            await app.EnLaBaseAsync(async db =>
            {
                var historial = await db.HistorialEstadosCheque.Where(h => h.ChequeId == s.Id).ToListAsync();
                var h = Assert.Single(historial);
                Assert.Equal(h.EstadoNuevo, (await db.Cheques.SingleAsync(c => c.Id == s.Id)).EstadoCheque);
                Assert.Equal(s.Usuario, h.UsuarioId); Assert.Equal(Ahora, h.OcurridoEn);
            });
        }
        finally { await app.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FalloDespuesDeEstadoOHistorial_RevierteAmbos(bool trasHistorial)
    {
        var app = new AplicacionFalloCheque(trasHistorial);
        try
        {
            await app.InitializeAsync();
            var s = await Preparar(app);
            app.Fallo.Activo = true;
            using (var scope = app.Services.CreateScope())
            {
                var error = await Assert.ThrowsAnyAsync<Exception>(() => scope.ServiceProvider.GetRequiredService<IRepositorioPagos>()
                    .CambiarEstadoAsync(s.Id, new(EstadoCheque.Pendiente, EstadoCheque.Entregado), s.Usuario, Hoy, Ahora));
                Assert.IsType<IOException>(error.GetBaseException());
            }
            Assert.True(app.Fallo.Disparado);
            await app.EnLaBaseAsync(async db =>
            {
                Assert.Equal(EstadoCheque.Pendiente, (await db.Cheques.SingleAsync(c => c.Id == s.Id)).EstadoCheque);
                Assert.Empty(await db.HistorialEstadosCheque.Where(h => h.ChequeId == s.Id).ToListAsync());
            });
        }
        finally { await app.DisposeAsync(); }
    }

    [Fact]
    public async Task RechazarOAnularIndividualmente_NoReabreFacturaNiOrden_BancosYTablasPaginan()
    {
        var app = new AplicacionDePrueba();
        try
        {
            await app.InitializeAsync();
            var s = await EscenarioPagos.CrearAsync(app);
            using var scope = app.Services.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IRepositorioPagos>();
            var entrada = new GenerarOrdenEntrada(Guid.NewGuid(), s.Orden.ProveedorId, Hoy, null, [s.Factura.Id],
                [new ChequeEntrada(Guid.NewGuid(), 60.50m, s.Banco.Id, "pago", Hoy, Hoy.AddDays(1), null),
                 new ChequeEntrada(Guid.NewGuid(), 60.50m, s.Banco.Id, "individual", Hoy, Hoy.AddDays(1), null)], 121m, true);
            await repo.GenerarAsync(entrada, s.UsuarioId, true, Hoy, Ahora);
            var id = await app.ConAlcanceAsync(db => db.Cheques.Where(c => c.BancoId == s.Banco.Id && c.NumeroCheque == "pago").Select(c => c.Id).SingleAsync());
            await repo.CambiarEstadoAsync(id, new(EstadoCheque.Pendiente, EstadoCheque.Entregado), s.UsuarioId, Hoy, Ahora);
            await repo.CambiarEstadoAsync(id, new(EstadoCheque.Entregado, EstadoCheque.Depositado), s.UsuarioId, Hoy, Ahora.AddSeconds(1));
            await repo.CambiarEstadoAsync(id, new(EstadoCheque.Depositado, EstadoCheque.Rechazado), s.UsuarioId, Hoy, Ahora.AddSeconds(2));
            var individual = await app.ConAlcanceAsync(db => db.Cheques.Where(c => c.BancoId == s.Banco.Id && c.NumeroCheque == "individual").Select(c => c.Id).SingleAsync());
            await repo.CambiarEstadoAsync(individual, new(EstadoCheque.Pendiente, EstadoCheque.Anulado), s.UsuarioId, Hoy, Ahora);
            await app.EnLaBaseAsync(async db =>
            {
                Assert.Equal(GT.Domain.Proveedores.EstadoFacturaProveedor.Pagada, (await db.FacturasProveedor.SingleAsync(f => f.Id == s.Factura.Id)).Estado);
                Assert.True((await db.OrdenPagoProveedorFacturas.SingleAsync(v => v.FacturaProveedorId == s.Factura.Id)).Vigente);
                var orden = await db.Cheques.Where(c => c.Id == id).Select(c => c.OrdenPagoProveedorId).SingleAsync();
                Assert.Equal(EstadoOrdenPagoProveedor.Pendiente, (await db.OrdenesPagoProveedor.SingleAsync(o => o.Id == orden)).Estado);
                Assert.Empty(await db.MovimientosDeCaja.Where(m => m.OrdenPagoProveedorId == orden).ToListAsync());
                for (var i = 0; i < 21; i++) db.Cheques.Add(new Cheque { OrdenPagoProveedorId = s.Orden.Id, BancoId = s.Banco.Id,
                    NumeroCheque = "pagina-" + i, Fecha = Hoy, FechaVencimiento = Hoy, Importe = 1m, RenglonId = Guid.NewGuid() });
                db.Bancos.Add(new Banco { Nombre = "Sin cheques " + Guid.NewGuid() });
                await db.SaveChangesAsync();
            });
            var filtro = new FiltroCheques(s.Banco.Id, null, null, null);
            var primera = await repo.ChequesAsync(filtro, Hoy);
            var segunda = await repo.ChequesAsync(filtro with { Pagina = 2 }, Hoy);
            Assert.Equal(23, primera.Total); Assert.Equal(20, primera.Items.Count); Assert.Equal(3, segunda.Items.Count);
            Assert.Empty(primera.Items.Select(c => c.Id).Intersect(segunda.Items.Select(c => c.Id)));
            Assert.DoesNotContain(await repo.BancosHistoricosAsync(), b => b.Nombre.StartsWith("Sin cheques ", StringComparison.Ordinal));
        }
        finally { await app.DisposeAsync(); }
    }

    private sealed class AplicacionFalloCheque(bool historial) : AplicacionDePrueba
    {
        public FalloCheque Fallo { get; } = new(historial);
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(s => s.AddDbContext<GtDbContext>(o => o.AddInterceptors(Fallo)));
        }
    }
    private sealed class FalloCheque(bool historial) : DbCommandInterceptor
    {
        public bool Activo;
        public bool Disparado;
        private int actualizaciones;
        private bool Coincide(DbCommand cmd)
        {
            if (!Activo || Disparado) return false;
            if (historial) return cmd.CommandText.Contains("INSERT INTO [HistorialEstadosCheque]", StringComparison.Ordinal);
            return cmd.CommandText.StartsWith("UPDATE", StringComparison.Ordinal) && cmd.CommandText.Contains("[MediosPagoProveedor]", StringComparison.Ordinal)
                && ++actualizaciones == 2;
        }
        public override ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (Coincide(command)) { Disparado = true; throw new IOException("Fallo después de estado SQL real"); }
            return ValueTask.FromResult(result);
        }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result, CancellationToken cancellationToken = default)
        {
            if (Coincide(command)) { Disparado = true; await result.DisposeAsync(); throw new IOException("Fallo después de historial SQL real"); }
            return result;
        }
    }
}
