using System.Text.Json;
using GT.Application.Pagos;
using GT.IntegrationTests.Caja;
using GT.Domain.Caja;
using GT.Domain.Pagos;
using GT.Domain.Proveedores;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

public class TransaccionAnularPagosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task EfectivoCompensaEnCajaOriginal_InclusoConOtroAnulador_UnSoloIngreso()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var operador = await app.UsuarioAsync(); var anulador = await app.UsuarioAsync();
        var caja = await app.CrearCajaAsync(operador.Id, saldoInicial: 121m);
        var hoy = new DateOnly(2026, 10, 18); var ahora = new DateTime(2026, 10, 18, 15, 0, 0, DateTimeKind.Utc);
        int id;
        using (var scope = app.Services.CreateScope())
        {
            var resultado = await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().GenerarAsync(new(Guid.NewGuid(), s.Orden.ProveedorId, hoy, null,
                [s.Factura.Id], [new EfectivoEntrada(Guid.NewGuid(), 60m), new EfectivoEntrada(Guid.NewGuid(), 61m)], 121m, true), operador.Id, false, hoy, ahora);
            using var json = JsonDocument.Parse(resultado.Json); id = json.RootElement.GetProperty("id").GetInt32();
        }
        var entrada = new AnularOrdenEntrada(Guid.NewGuid(), "Corregir pago", true);
        using (var scope = app.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRepositorioPagos>();
            var original = await repo.AnularAsync(id, entrada, anulador.Id, false, hoy, ahora.AddMinutes(1));
            Assert.Equal(original.Json, (await repo.AnularAsync(id, entrada, anulador.Id, false, hoy, ahora.AddMinutes(2))).Json);
            using var json = JsonDocument.Parse(original.Json);
            Assert.Equal("121.00", json.RootElement.GetProperty("caja").GetProperty("ingresoCompensatorio").GetProperty("importe").GetString());
        }
        await app.EnLaBaseAsync(async db =>
        {
            var movimientos = await db.MovimientosDeCaja.Where(m => m.OrdenPagoProveedorId == id).ToListAsync();
            Assert.Equal(2, movimientos.Count); Assert.All(movimientos, m => { Assert.Equal(caja.Id, m.CajaId); Assert.Null(m.OrdenDePagoId); Assert.Equal(121m, m.Importe); });
            Assert.Equal(operador.Id, Assert.Single(movimientos, m => m.Tipo == TipoMovimientoCaja.Egreso).UsuarioId);
            var ingreso = Assert.Single(movimientos, m => m.Tipo == TipoMovimientoCaja.Ingreso);
            Assert.Equal(anulador.Id, ingreso.UsuarioId); Assert.StartsWith("Anulación OPP-", ingreso.Concepto);
        });
    }
    [Fact]
    public async Task AnularConChequeConservaHistoria_LiberaFactura_YReplayNoRepite()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var hoy = new DateOnly(2026, 10, 18); var ahora = new DateTime(2026, 10, 18, 15, 0, 0, DateTimeKind.Utc);
        int id;
        using (var scope = app.Services.CreateScope())
        {
            var generar = await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().GenerarAsync(new(Guid.NewGuid(), s.Orden.ProveedorId, hoy, null,
                [s.Factura.Id], [new ChequeEntrada(Guid.NewGuid(), 121m, s.Banco.Id, "pago", hoy, hoy.AddDays(1), null)], 121m, true), s.UsuarioId, true, hoy, ahora);
            using var json = JsonDocument.Parse(generar.Json); id = json.RootElement.GetProperty("id").GetInt32();
        }
        var entrada = new AnularOrdenEntrada(Guid.NewGuid(), "  Corrección  ", true);
        string original;
        using (var scope = app.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRepositorioPagos>();
            var resultado = await repo.AnularAsync(id, entrada, s.UsuarioId, false, hoy, ahora.AddMinutes(1));
            Assert.Equal(200, resultado.EstadoHttp); original = resultado.Json;
            using var json = JsonDocument.Parse(original);
            Assert.Equal("anulada", json.RootElement.GetProperty("estado").GetString());
            Assert.Equal("Corrección", json.RootElement.GetProperty("anulacion").GetProperty("motivo").GetString());
            Assert.Equal(original, (await repo.AnularAsync(id, entrada, s.UsuarioId, false, hoy.AddDays(1), ahora.AddDays(1))).Json);
        }
        await app.EnLaBaseAsync(async db =>
        {
            Assert.Equal(EstadoFacturaProveedor.Pendiente, (await db.FacturasProveedor.SingleAsync(f => f.Id == s.Factura.Id)).Estado);
            Assert.False((await db.OrdenPagoProveedorFacturas.SingleAsync(v => v.FacturaProveedorId == s.Factura.Id)).Vigente);
            var c = await db.Cheques.SingleAsync(c => c.OrdenPagoProveedorId == id);
            Assert.Equal(EstadoCheque.Anulado, c.EstadoCheque); Assert.Single(await db.HistorialEstadosCheque.Where(h => h.ChequeId == c.Id).ToListAsync());
            Assert.Single(await db.ResultadosOperacionPago.Where(r => r.OrdenPagoProveedorId == id && r.Operacion == TipoOperacionPago.Anular).ToListAsync());
        });
    }
}
