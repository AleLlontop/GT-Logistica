using GT.Application.Pagos;
using GT.Domain.Caja;
using GT.Domain.Liquidaciones;
using GT.Domain.Pagos;
using GT.Domain.Proveedores;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

public class AnularOrdenTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task SoloRevierteOPPropias_ConservaPagosManualesYDeOtraOPP()
    {
        var propia = await EscenarioAnulacion.CrearAsync(app, "fletero");
        var ajena = await EscenarioAnulacion.CrearAsync(app, "fletero");
        var manualId = await app.ConAlcanceAsync(async db =>
        {
            var transportista = await db.Liquidaciones.Where(l => l.Id == ajena.Liquidaciones[0]).Select(l => l.TransportistaId).SingleAsync();
            var l = new Liquidacion { TransportistaId = transportista, PeriodoMes = 9, PeriodoAnio = 2026, ImporteTotal = 121m, ImportePagado = 50m };
            var pago = new OrdenDePago { Liquidacion = l, LiquidacionId = 0, FechaPago = EscenarioAnulacion.Hoy, Importe = 50m,
                UsuarioId = propia.UsuarioId, RegistradaEn = EscenarioAnulacion.Ahora };
            db.Add(pago); await db.SaveChangesAsync(); return pago.Id;
        });
        using var scope = app.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().AnularAsync(propia.OrdenId, new(Guid.NewGuid(), "Corregir propia", true),
            propia.UsuarioId, true, EscenarioAnulacion.Hoy, EscenarioAnulacion.Ahora.AddMinutes(1));
        await app.EnLaBaseAsync(async db =>
        {
            var manual = await db.OrdenesDePago.SingleAsync(p => p.Id == manualId);
            Assert.True(manual.Vigente); Assert.Null(manual.OrdenPagoProveedorId); Assert.Null(manual.RevertidaEn); Assert.Equal(50m, manual.Importe);
            Assert.Equal(50m, (await db.Liquidaciones.SingleAsync(l => l.Id == manual.LiquidacionId)).ImportePagado);
            var otros = await db.OrdenesDePago.Where(p => p.OrdenPagoProveedorId == ajena.OrdenId).ToListAsync();
            Assert.Equal(3, otros.Count); Assert.All(otros, p => { Assert.True(p.Vigente); Assert.Null(p.RevertidaEn); });
            Assert.All(await db.Liquidaciones.Where(l => ajena.Liquidaciones.Contains(l.Id)).ToListAsync(), l => Assert.Equal(EstadoLiquidacion.Pagada, l.Estado));
        });
    }
    [Theory]
    [InlineData("cheque")]
    [InlineData("efectivo")]
    [InlineData("fletero")]
    public async Task AnulacionConservaTodosLosOriginales_YRestauraObligaciones(string tipo)
    {
        var s = await EscenarioAnulacion.CrearAsync(app, tipo);
        using var scope = app.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRepositorioPagos>();
        await repo.AnularAsync(s.OrdenId, new(Guid.NewGuid(), "Corrección", true), s.UsuarioId, true, EscenarioAnulacion.Hoy, EscenarioAnulacion.Ahora.AddMinutes(1));
        await app.EnLaBaseAsync(async db =>
        {
            Assert.Equal(EstadoOrdenPagoProveedor.Anulada, (await db.OrdenesPagoProveedor.SingleAsync(o => o.Id == s.OrdenId)).Estado);
            Assert.All(await db.FacturasProveedor.Where(f => s.Facturas.Contains(f.Id)).ToListAsync(), f => Assert.Equal(EstadoFacturaProveedor.Pendiente, f.Estado));
            Assert.All(await db.OrdenPagoProveedorFacturas.Where(v => v.OrdenPagoProveedorId == s.OrdenId).ToListAsync(), v => Assert.False(v.Vigente));
            var cheques = await db.Cheques.Where(c => c.OrdenPagoProveedorId == s.OrdenId).ToListAsync();
            Assert.All(cheques, c => Assert.Equal(EstadoCheque.Anulado, c.EstadoCheque));
            foreach (var c in cheques) Assert.Single(await db.HistorialEstadosCheque.Where(h => h.ChequeId == c.Id).ToListAsync());
            if (tipo == "fletero")
            {
                var pagos = await db.OrdenesDePago.Where(p => p.OrdenPagoProveedorId == s.OrdenId).ToListAsync();
                Assert.Equal(3, pagos.Count); Assert.All(pagos, p => { Assert.False(p.Vigente); Assert.Equal(s.UsuarioId, p.RevertidaPorId); Assert.NotNull(p.RevertidaEn); });
                Assert.All(await db.Liquidaciones.Where(l => s.Liquidaciones.Contains(l.Id)).ToListAsync(), l => { Assert.Equal(EstadoLiquidacion.Pendiente, l.Estado); Assert.Equal(0m, l.ImportePagado); });
                foreach (var p in pagos)
                {
                    var eventos = await db.CambiosDeLiquidacion.Where(h => h.OrdenDePagoId == p.Id).ToListAsync();
                    Assert.Contains(eventos, h => h.Operacion == OperacionDeLiquidacion.PagoPorOPP);
                    Assert.Contains(eventos, h => h.Operacion == OperacionDeLiquidacion.ReversionPagoPorOPP);
                }
            }
            var movimientos = await db.MovimientosDeCaja.Where(m => m.OrdenPagoProveedorId == s.OrdenId).ToListAsync();
            if (tipo == "efectivo") { Assert.Equal(2, movimientos.Count); Assert.Equal(121m, Assert.Single(movimientos, m => m.Tipo == TipoMovimientoCaja.Ingreso).Importe); }
            else Assert.Empty(movimientos);
        });
        Assert.Equal(s.Facturas.Length, (await repo.SeleccionAsync(s.Facturas)).Items.Count);
    }

    [Theory]
    [InlineData("transferencia")]
    [InlineData("mixta")]
    [InlineData("caja_cerrada")]
    [InlineData("entregado")]
    [InlineData("depositado")]
    [InlineData("cobrado")]
    [InlineData("rechazado")]
    [InlineData("anulado")]
    [InlineData("reversion_imposible")]
    public async Task CondicionNoAnulableRechazaConjuntoSinCompensacion(string caso)
    {
        var tipo = caso == "caja_cerrada" ? "efectivo" : caso == "reversion_imposible" ? "fletero" : caso is "transferencia" or "mixta" ? caso : "cheque";
        var s = await EscenarioAnulacion.CrearAsync(app, tipo);
        await app.EnLaBaseAsync(async db =>
        {
            if (caso == "caja_cerrada") await db.Cajas.Where(c => c.Id == s.CajaId).ExecuteUpdateAsync(u => u.SetProperty(c => c.Estado, EstadoCaja.Cerrada)
                .SetProperty(c => c.FechaCierre, EscenarioAnulacion.Ahora).SetProperty(c => c.SaldoFinal, 0m));
            else if (caso == "reversion_imposible") await db.Liquidaciones.Where(l => l.Id == s.Liquidaciones[0]).ExecuteUpdateAsync(u => u.SetProperty(l => l.ImporteTotal, 122m).SetProperty(l => l.Estado, EstadoLiquidacion.Pendiente));
            else if (Enum.TryParse<EstadoCheque>(caso, true, out var estado)) await db.Cheques.Where(c => c.OrdenPagoProveedorId == s.OrdenId).ExecuteUpdateAsync(u => u.SetProperty(c => c.EstadoCheque, estado));
        });
        using var scope = app.Services.CreateScope();
        var error = await Assert.ThrowsAsync<ReglaPagoException>(() => scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().AnularAsync(s.OrdenId,
            new(Guid.NewGuid(), "Motivo", true), s.UsuarioId, true, EscenarioAnulacion.Hoy, EscenarioAnulacion.Ahora.AddMinutes(1)));
        Assert.Equal(409, error.EstadoHttp);
        await app.EnLaBaseAsync(async db =>
        {
            Assert.Equal(EstadoOrdenPagoProveedor.Pendiente, (await db.OrdenesPagoProveedor.SingleAsync(o => o.Id == s.OrdenId)).Estado);
            Assert.All(await db.FacturasProveedor.Where(f => s.Facturas.Contains(f.Id)).ToListAsync(), f => Assert.Equal(EstadoFacturaProveedor.Pagada, f.Estado));
            Assert.All(await db.OrdenesDePago.Where(p => p.OrdenPagoProveedorId == s.OrdenId).ToListAsync(), p => Assert.True(p.Vigente));
            Assert.Empty(await db.MovimientosDeCaja.Where(m => m.OrdenPagoProveedorId == s.OrdenId && m.Tipo == TipoMovimientoCaja.Ingreso).ToListAsync());
            Assert.Empty(await db.ResultadosOperacionPago.Where(r => r.OrdenPagoProveedorId == s.OrdenId && r.Operacion == TipoOperacionPago.Anular).ToListAsync());
        });
    }
}
