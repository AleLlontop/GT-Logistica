using GT.Domain.Choferes;
using GT.Domain.Liquidaciones;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Pagos;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Liquidaciones;

public class PrimitivasPagoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private async Task<Liquidacion> PendienteAsync()
    {
        var (cliente, transportista) = await app.EscenarioBaseAsync();
        var viaje = await app.ViajeDeAsync(cliente, transportista.Id, 121m);
        return await app.CrearLiquidacionAsync(transportista.Id, [viaje]);
    }

    [Fact]
    public async Task ExigeTransaccionDelLlamador()
    {
        await app.EnLaBaseAsync(async db =>
            await Assert.ThrowsAsync<InvalidOperationException>(() => new PrimitivasPagosLiquidacion(db)
                .RegistrarAsync(1, new(2026,10,18), 1m, 1, DateTime.UtcNow)));
    }

    [Fact]
    public async Task PagoManualParcialYTotal_ConservaContratoYEventoHistorico()
    {
        var liquidacion = await PendienteAsync();
        var usuario = await app.ObtenerAdministradorAsync();
        await app.EnLaBaseAsync(async db =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var p = new PrimitivasPagosLiquidacion(db);
            var parcial = await p.RegistrarAsync(liquidacion.Id, new(2026,10,18), 30m, usuario.Id, DateTime.UtcNow);
            Assert.NotNull(parcial);
            Assert.Null(parcial.OrdenPagoProveedorId);
            Assert.Equal(EstadoLiquidacion.Pendiente, await db.Liquidaciones.Where(x => x.Id == liquidacion.Id).Select(x => x.Estado).SingleAsync());
            Assert.NotNull(await p.RegistrarAsync(liquidacion.Id, new(2026,10,18), 91m, usuario.Id, DateTime.UtcNow));
            Assert.Null(await p.RegistrarAsync(liquidacion.Id, new(2026,10,18), .01m, usuario.Id, DateTime.UtcNow));
            await tx.CommitAsync();
        });
        await app.EnLaBaseAsync(async db =>
        {
            var actual = await db.Liquidaciones.SingleAsync(x => x.Id == liquidacion.Id);
            Assert.Equal(EstadoLiquidacion.Pagada, actual.Estado);
            Assert.Equal(121m, actual.ImportePagado);
            Assert.Equal(2, await db.OrdenesDePago.CountAsync(x => x.LiquidacionId == liquidacion.Id && x.Vigente && x.OrdenPagoProveedorId == null));
            Assert.Equal(1, await db.CambiosDeLiquidacion.CountAsync(x => x.LiquidacionId == liquidacion.Id && x.Operacion == OperacionDeLiquidacion.Pagada));
        });
    }

    [Fact]
    public async Task RollbackExterior_NoDejaPagoAgregadoNiEvento()
    {
        var liquidacion = await PendienteAsync();
        var usuario = await app.ObtenerAdministradorAsync();
        await app.EnLaBaseAsync(async db =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            Assert.NotNull(await new PrimitivasPagosLiquidacion(db).RegistrarAsync(liquidacion.Id, new(2026,10,18), 121m, usuario.Id, DateTime.UtcNow));
            await tx.RollbackAsync();
        });
        await app.EnLaBaseAsync(async db =>
        {
            Assert.Equal(0m, await db.Liquidaciones.Where(x => x.Id == liquidacion.Id).Select(x => x.ImportePagado).SingleAsync());
            Assert.False(await db.OrdenesDePago.AnyAsync(x => x.LiquidacionId == liquidacion.Id));
            Assert.False(await db.CambiosDeLiquidacion.AnyAsync(x => x.LiquidacionId == liquidacion.Id && x.Operacion == OperacionDeLiquidacion.Pagada));
        });
    }

    [Fact]
    public async Task Reversion_SoloOrigenPropio_ConservaHistoriaYPermiteNuevoPago()
    {
        var liquidacion = await PendienteAsync();
        var escenario = await EscenarioPagos.CrearAsync(app);
        await app.EnLaBaseAsync(async db =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var p = new PrimitivasPagosLiquidacion(db);
            var op = (await p.RegistrarAsync(liquidacion.Id, new(2026,10,18), 121m, escenario.UsuarioId, DateTime.UtcNow, escenario.Orden.Id))!;
            await Assert.ThrowsAsync<InvalidOperationException>(() => p.RevertirAsync(op, int.MaxValue, escenario.UsuarioId, DateTime.UtcNow));
            await p.RevertirAsync(op, escenario.Orden.Id, escenario.UsuarioId, DateTime.UtcNow);
            await tx.CommitAsync();
        });
        await app.EnLaBaseAsync(async db =>
        {
            Assert.Equal(0m, await db.Liquidaciones.Where(x => x.Id == liquidacion.Id).Select(x => x.ImportePagado).SingleAsync());
            Assert.False((await db.OrdenesDePago.SingleAsync(x => x.LiquidacionId == liquidacion.Id)).Vigente);
            Assert.Equal(2, await db.CambiosDeLiquidacion.CountAsync(x => x.LiquidacionId == liquidacion.Id && x.OrdenDePagoId != null));
            await using var tx = await db.Database.BeginTransactionAsync();
            Assert.NotNull(await new PrimitivasPagosLiquidacion(db).RegistrarAsync(liquidacion.Id, new(2026,10,18), 121m, escenario.UsuarioId, DateTime.UtcNow));
            await tx.CommitAsync();
        });
    }
}
