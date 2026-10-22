using GT.Domain.Pagos;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Pagos;

public class BloqueosPagosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task ExigeTransaccionYRechazaInversionesDelOrdenGlobal()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        await app.EnLaBaseAsync(async db =>
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => new BloqueosPagos(db).OrdenAsync(s.Orden.Id));
            await using var tx = await db.Database.BeginTransactionAsync();
            var locks = new BloqueosPagos(db);
            await locks.IdempotenciaAsync(TipoOperacionPago.Generar, Guid.NewGuid());
            Assert.Equal(1, await locks.OrdenAsync(s.Orden.Id));
            await locks.BancosAsync([s.Banco.Id]);
            await locks.FacturasAsync([s.Factura.Id], paraGenerar: true);
            await Assert.ThrowsAsync<InvalidOperationException>(() => locks.BancosAsync([s.Banco.Id]));
            await tx.RollbackAsync();
        });
    }

    [Fact]
    public async Task FacturaDeOtraOrdenVigenteEsConflictoCompleto()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        await app.EnLaBaseAsync(async db =>
        {
            db.OrdenPagoProveedorFacturas.Add(new() { OrdenPagoProveedorId = s.Orden.Id,
                FacturaProveedorId = s.Factura.Id, ImporteTotalAlPagar = 121m });
            await db.SaveChangesAsync();
            await using var tx = await db.Database.BeginTransactionAsync();
            var locks = new BloqueosPagos(db);
            var error = await Assert.ThrowsAsync<GT.Application.Pagos.ReglaPagoException>(() => locks.FacturasAsync([s.Factura.Id], true));
            Assert.Equal(409, error.EstadoHttp);
            Assert.Equal("facturas_no_disponibles", error.Codigo);
            await tx.RollbackAsync();
        });
    }
}
