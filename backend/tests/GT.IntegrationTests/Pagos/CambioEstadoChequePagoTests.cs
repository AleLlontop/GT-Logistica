using GT.Application.Pagos;
using GT.Domain.Pagos;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

public class CambioEstadoChequePagoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task CambioReleeHistorialCompleto_ConflictoNoAgregaHistoriaNiModificaDeuda()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var id = await app.ConAlcanceAsync(async db =>
        {
            var c = new Cheque { OrdenPagoProveedorId = s.Orden.Id, BancoId = s.Banco.Id,
                RenglonId = Guid.NewGuid(), NumeroCheque = Guid.NewGuid().ToString(), Importe = 121m,
                Fecha = new(2026, 10, 18), FechaVencimiento = new(2026, 10, 20) };
            db.Add(c); await db.SaveChangesAsync(); return c.Id;
        });
        using var scope = app.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRepositorioPagos>();
        var hoy = new DateOnly(2026, 10, 18);
        var ahora = new DateTime(2026, 10, 18, 15, 0, 0, DateTimeKind.Utc);
        var primero = await repo.CambiarEstadoAsync(id, new(EstadoCheque.Pendiente, EstadoCheque.Entregado), s.UsuarioId, hoy, ahora);
        Assert.NotNull(primero); Assert.Single(primero.Historial);
        var segundo = await repo.CambiarEstadoAsync(id, new(EstadoCheque.Entregado, EstadoCheque.Anulado), s.UsuarioId, hoy, ahora.AddMinutes(1));
        Assert.NotNull(segundo); Assert.Equal(EstadoCheque.Anulado, segundo.Estado); Assert.Equal(2, segundo.Historial.Count);
        var error = await Assert.ThrowsAsync<ReglaPagoException>(() => repo.CambiarEstadoAsync(id,
            new(EstadoCheque.Entregado, EstadoCheque.Depositado), s.UsuarioId, hoy, ahora));
        Assert.Equal(409, error.EstadoHttp);
        Assert.Equal(2, (await repo.ChequeAsync(id, hoy))!.Historial.Count);
        await app.EnLaBaseAsync(async db =>
        {
            Assert.Equal(EstadoOrdenPagoProveedor.Pendiente, (await db.OrdenesPagoProveedor.SingleAsync(o => o.Id == s.Orden.Id)).Estado);
            Assert.Empty(await db.MovimientosDeCaja.Where(m => m.OrdenPagoProveedorId == s.Orden.Id).ToListAsync());
        });
    }
}
