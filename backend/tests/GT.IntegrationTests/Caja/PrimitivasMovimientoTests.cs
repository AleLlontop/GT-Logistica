using GT.Domain.Caja;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Pagos;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Caja;

public class PrimitivasMovimientoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task ExigeTransaccionDelLlamador() =>
        await app.EnLaBaseAsync(async db =>
            await Assert.ThrowsAsync<InvalidOperationException>(() => new PrimitivasMovimientosCaja(db).TomarAsync(1, 1)));

    [Fact]
    public async Task ManualConservaResponsableYSaldoNegativo_RollbackExternoNoPersiste()
    {
        var usuario = await app.UsuarioAsync();
        var caja = await app.CrearCajaAsync(usuario.Id, saldoInicial: 100m);
        await app.EnLaBaseAsync(async db =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var p = new PrimitivasMovimientosCaja(db);
            Assert.Equal(0, await p.TomarAsync(caja.Id, int.MaxValue));
            Assert.Equal(1, await p.TomarAsync(caja.Id, usuario.Id));
            await p.InsertarAsync(new MovimientoDeCaja { CajaId = caja.Id, Tipo = TipoMovimientoCaja.Egreso,
                Importe = 110m, Concepto = "Egreso manual", UsuarioId = usuario.Id, Fecha = DateTime.UtcNow });
            Assert.Equal(-10m, await p.SaldoAsync(caja.Id));
            await tx.RollbackAsync();
        });
        Assert.Equal(0, await app.ContarMovimientosDeAsync(caja.Id));
    }

    [Fact]
    public async Task CompensacionInternaPermiteOtroAnuladorSobreCajaOriginalAbierta()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var responsable = await app.UsuarioAsync();
        var caja = await app.CrearCajaAsync(responsable.Id, saldoInicial: 121m);
        await app.EnLaBaseAsync(async db =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            var p = new PrimitivasMovimientosCaja(db);
            Assert.Equal(1, await p.TomarAsync(caja.Id, responsable.Id));
            await p.InsertarAsync(new MovimientoDeCaja { CajaId = caja.Id, Tipo = TipoMovimientoCaja.Egreso,
                Importe = 121m, Concepto = $"Pago OPP-{s.Orden.Numero}", UsuarioId = responsable.Id,
                Fecha = DateTime.UtcNow, OrdenPagoProveedorId = s.Orden.Id });
            Assert.Equal(0m, await p.SaldoAsync(caja.Id));
            Assert.Equal(1, await p.TomarAsync(caja.Id, null));
            await p.InsertarAsync(new MovimientoDeCaja { CajaId = caja.Id, Tipo = TipoMovimientoCaja.Ingreso,
                Importe = 121m, Concepto = $"Anulación OPP-{s.Orden.Numero}", UsuarioId = s.UsuarioId,
                Fecha = DateTime.UtcNow, OrdenPagoProveedorId = s.Orden.Id });
            Assert.Equal(121m, await p.SaldoAsync(caja.Id));
            await tx.CommitAsync();
        });
        Assert.Equal(2, await app.ContarMovimientosDeAsync(caja.Id));
    }

    [Fact]
    public async Task CajaCerradaNoAdmiteLockNiCompensacion()
    {
        var usuario = await app.UsuarioAsync();
        var caja = await app.CrearCajaAsync(usuario.Id, estado: EstadoCaja.Cerrada);
        await app.EnLaBaseAsync(async db =>
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            Assert.Equal(0, await new PrimitivasMovimientosCaja(db).TomarAsync(caja.Id, null));
            await tx.RollbackAsync();
        });
    }
}
