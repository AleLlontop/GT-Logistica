using GT.Application.Caja;
using GT.Application.Liquidaciones;
using GT.Application.Pagos;
using GT.Domain.Caja;
using GT.Domain.Pagos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

public class ConcurrenciaAnularTests
{
    private static async Task<int> Anular(ConcurrenciaGenerarTests.AplicacionConBarrera app, PagoParaAnular s)
    {
        using var scope = app.Services.CreateScope();
        try { return (await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().AnularAsync(s.OrdenId,
            new(Guid.NewGuid(), "Corrección", true), s.UsuarioId, true, EscenarioAnulacion.Hoy, EscenarioAnulacion.Ahora.AddMinutes(1))).EstadoHttp; }
        catch (ReglaPagoException e) { return e.EstadoHttp; }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnularContraChequeODosAnulaciones_SoloUnaTransicion(bool dosAnulaciones)
    {
        var app = new ConcurrenciaGenerarTests.AplicacionConBarrera("[OrdenesPagoProveedor]");
        try
        {
            await app.InitializeAsync(); var s = await EscenarioAnulacion.CrearAsync(app);
            var cheque = await app.ConAlcanceAsync(db => db.Cheques.Where(c => c.OrdenPagoProveedorId == s.OrdenId).Select(c => c.Id).SingleAsync());
            async Task<int> Cambiar()
            {
                using var scope = app.Services.CreateScope();
                try { await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().CambiarEstadoAsync(cheque,
                    new(EstadoCheque.Pendiente, EstadoCheque.Entregado), s.UsuarioId, EscenarioAnulacion.Hoy, EscenarioAnulacion.Ahora.AddMinutes(1)); return 200; }
                catch (ReglaPagoException e) { return e.EstadoHttp; }
            }
            app.Barrera.Activa = true;
            var resultados = await Task.WhenAll(Anular(app, s), dosAnulaciones ? Anular(app, s) : Cambiar()).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(2, app.Barrera.Llegadas); Assert.Equal(new[] { 200, 409 }, resultados.Order().ToArray());
            await app.EnLaBaseAsync(async db =>
            {
                var actual = await db.Cheques.SingleAsync(c => c.Id == cheque);
                Assert.Single(await db.HistorialEstadosCheque.Where(h => h.ChequeId == cheque).ToListAsync());
                var anulada = (await db.OrdenesPagoProveedor.SingleAsync(o => o.Id == s.OrdenId)).Estado == EstadoOrdenPagoProveedor.Anulada;
                Assert.Equal(anulada ? EstadoCheque.Anulado : EstadoCheque.Entregado, actual.EstadoCheque);
                Assert.Equal(anulada ? GT.Domain.Proveedores.EstadoFacturaProveedor.Pendiente : GT.Domain.Proveedores.EstadoFacturaProveedor.Pagada,
                    (await db.FacturasProveedor.SingleAsync(f => f.Id == s.Facturas[0])).Estado);
            });
        }
        finally { await app.DisposeAsync(); }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CierreOMovimientoManualContraCompensacion_ComparteLockCaja(bool cerrar)
    {
        var app = new ConcurrenciaGenerarTests.AplicacionConBarrera("[Cajas]");
        try
        {
            await app.InitializeAsync(); var s = await EscenarioAnulacion.CrearAsync(app, "efectivo");
            async Task<bool> Manual()
            {
                using var scope = app.Services.CreateScope(); var repo = scope.ServiceProvider.GetRequiredService<IRepositorioCaja>();
                if (cerrar) return (await repo.CerrarAsync(s.CajaId!.Value, s.UsuarioId, 0m, EscenarioAnulacion.Ahora.AddMinutes(1))).Cerrada;
                return (await repo.RegistrarMovimientoAsync(new() { CajaId = s.CajaId!.Value, Tipo = TipoMovimientoCaja.Ingreso, Importe = 10m,
                    Concepto = "Ingreso manual", UsuarioId = s.UsuarioId, Fecha = EscenarioAnulacion.Ahora.AddMinutes(1) })).Id.HasValue;
            }
            app.Barrera.Activa = true; var anulacion = Anular(app, s); var manual = Manual();
            await Task.WhenAll(anulacion, (Task)manual).WaitAsync(TimeSpan.FromSeconds(30));
            var estado = await anulacion; var manualOk = await manual; Assert.Equal(2, app.Barrera.Llegadas);
            if (cerrar) Assert.NotEqual(estado == 200, manualOk); else { Assert.Equal(200, estado); Assert.True(manualOk); }
            await app.EnLaBaseAsync(async db => Assert.Equal(estado == 200 ? 1 : 0, await db.MovimientosDeCaja.CountAsync(m => m.OrdenPagoProveedorId == s.OrdenId && m.Tipo == TipoMovimientoCaja.Ingreso)));
        }
        finally { await app.DisposeAsync(); }
    }
    [Theory]
    [InlineData("pago")]
    [InlineData("edicion")]
    [InlineData("anulacion")]
    public async Task Operacion009ContraAnulacionOPP_SerializaSinRevertirPagosAjenos(string caso)
    {
        var app = new ConcurrenciaGenerarTests.AplicacionConBarrera("[Liquidaciones]");
        try
        {
            await app.InitializeAsync(); var s = await EscenarioAnulacion.CrearAsync(app, "fletero");
            var liquidacion = s.Liquidaciones.Min();
            async Task<bool> Manual()
            {
                using var scope = app.Services.CreateScope(); var repo = scope.ServiceProvider.GetRequiredService<IRepositorioLiquidaciones>();
                return caso switch
                {
                    "pago" => await repo.RegistrarPagoAsync(liquidacion, EscenarioAnulacion.Hoy, 50m, s.UsuarioId, EscenarioAnulacion.Ahora.AddMinutes(1)) is not null,
                    "edicion" => await repo.EditarAsync(liquidacion, 0, 121m, [], [], s.UsuarioId, EscenarioAnulacion.Ahora.AddMinutes(1)),
                    _ => await repo.AnularAsync(liquidacion, "Anulación manual posterior", s.UsuarioId, EscenarioAnulacion.Ahora.AddMinutes(1)),
                };
            }
            app.Barrera.Activa = true; var anulacion = Anular(app, s); var manual = Manual();
            await Task.WhenAll(anulacion, (Task)manual).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(200, await anulacion); Assert.Equal(2, app.Barrera.Llegadas);
            var manualOk = await manual;
            await app.EnLaBaseAsync(async db =>
            {
                Assert.All(await db.OrdenesDePago.Where(p => p.OrdenPagoProveedorId == s.OrdenId).ToListAsync(), p => Assert.False(p.Vigente));
                var l = await db.Liquidaciones.SingleAsync(l => l.Id == liquidacion);
                Assert.Equal(caso == "pago" && manualOk ? 50m : 0m, l.ImportePagado);
                if (caso == "pago" && manualOk) Assert.Single(await db.OrdenesDePago.Where(p => p.LiquidacionId == liquidacion && p.OrdenPagoProveedorId == null && p.Vigente).ToListAsync());
                if (caso == "edicion") Assert.Equal(manualOk ? 1 : 0, l.Version);
                if (caso == "anulacion") Assert.Equal(manualOk ? GT.Domain.Liquidaciones.EstadoLiquidacion.Anulada : GT.Domain.Liquidaciones.EstadoLiquidacion.Pendiente, l.Estado);
            });
        }
        finally { await app.DisposeAsync(); }
    }

}
