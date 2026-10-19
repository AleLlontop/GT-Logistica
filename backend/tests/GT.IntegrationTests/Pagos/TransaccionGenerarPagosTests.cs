using System.Text.Json;
using GT.Application.Pagos;
using GT.Domain.Pagos;
using GT.Domain.Liquidaciones;
using GT.IntegrationTests.Proveedores;
using GT.Domain.Proveedores;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Caja;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

public class TransaccionGenerarPagosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task GenerarCheque_ReleeDetalleYResultadoAtomico_ReplayAntesDeElegibilidad()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var hoy = new DateOnly(2026, 10, 18);
        var entrada = new GenerarOrdenEntrada(Guid.NewGuid(), s.Orden.ProveedorId, hoy.AddDays(-1), "  Servicios  ",
            [s.Factura.Id], [new ChequeEntrada(Guid.NewGuid(), 121m, s.Banco.Id, "  000-123  ", hoy, hoy, null)], 121m, true);
        using var scope = app.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRepositorioPagos>();
        var resultado = await repo.GenerarAsync(entrada, s.UsuarioId, true, hoy, new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc));
        Assert.Equal(201, resultado.EstadoHttp);
        using var json = JsonDocument.Parse(resultado.Json);
        var id = json.RootElement.GetProperty("id").GetInt32();
        Assert.StartsWith("OPP-", json.RootElement.GetProperty("numero").GetString());
        Assert.Equal("121.00", json.RootElement.GetProperty("importeTotal").GetString());
        Assert.Equal("Servicios", json.RootElement.GetProperty("detalle").GetString());
        Assert.Equal("000-123", Assert.Single(json.RootElement.GetProperty("medios").EnumerateArray()).GetProperty("numeroCheque").GetString());
        await app.EnLaBaseAsync(async db =>
        {
            Assert.Equal(EstadoFacturaProveedor.Pagada, (await db.FacturasProveedor.SingleAsync(x => x.Id == s.Factura.Id)).Estado);
            Assert.True((await db.OrdenPagoProveedorFacturas.SingleAsync(x => x.OrdenPagoProveedorId == id)).Vigente);
            Assert.Single(await db.ResultadosOperacionPago.Where(x => x.OrdenPagoProveedorId == id).ToListAsync());
            Assert.Empty(await db.MovimientosDeCaja.Where(x => x.OrdenPagoProveedorId == id).ToListAsync());
        });
        var replay = await repo.GenerarAsync(entrada, s.UsuarioId, true, hoy.AddDays(1), new(2026, 10, 19, 12, 0, 0, DateTimeKind.Utc));
        Assert.Equal(resultado, replay);
    }

    [Fact]
    public async Task TotalClienteNoAutoritativo_NoPagaConSumaInexactaNiConsumeClave()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var hoy = new DateOnly(2026, 10, 18);
        var entrada = new GenerarOrdenEntrada(Guid.NewGuid(), s.Orden.ProveedorId, hoy, null,
            [s.Factura.Id], [new ChequeEntrada(Guid.NewGuid(), 120m, s.Banco.Id, "123", hoy, hoy, null)], 120m, true);
        using var scope = app.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IRepositorioPagos>();
        var error = await Assert.ThrowsAsync<ReglaPagoException>(() => repo.GenerarAsync(entrada, s.UsuarioId, true, hoy, DateTime.UtcNow));
        Assert.Equal(400, error.EstadoHttp);
        await app.EnLaBaseAsync(async db =>
        {
            Assert.Equal(EstadoFacturaProveedor.Pendiente, (await db.FacturasProveedor.SingleAsync(x => x.Id == s.Factura.Id)).Estado);
            Assert.Empty(await db.ResultadosOperacionPago.Where(x => x.Clave == entrada.ClaveOperacion).ToListAsync());
            Assert.Empty(await db.OrdenPagoProveedorFacturas.Where(x => x.FacturaProveedorId == s.Factura.Id).ToListAsync());
        });
    }
    [Fact]
    public async Task EfectivoDosRenglones_UnEgresoYDetalleCompleto()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var caja = await app.CrearCajaAsync(s.UsuarioId, saldoInicial: 121m);
        var hoy = new DateOnly(2026, 10, 18);
        using var scope = app.Services.CreateScope();
        var resultado = await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().GenerarAsync(
            new(Guid.NewGuid(), s.Orden.ProveedorId, hoy, null, [s.Factura.Id],
                [new EfectivoEntrada(Guid.NewGuid(), 60m), new EfectivoEntrada(Guid.NewGuid(), 61m)], 121m, true),
            s.UsuarioId, true, hoy, DateTime.UtcNow);
        using var json = JsonDocument.Parse(resultado.Json);
        var id = json.RootElement.GetProperty("id").GetInt32();
        Assert.Equal(2, json.RootElement.GetProperty("medios").GetArrayLength());
        Assert.Equal(caja.Id, json.RootElement.GetProperty("caja").GetProperty("id").GetInt32());
        await app.EnLaBaseAsync(async db =>
        {
            var movimiento = Assert.Single(await db.MovimientosDeCaja.Where(x => x.OrdenPagoProveedorId == id).ToListAsync());
            Assert.Equal(121m, movimiento.Importe);
            Assert.Equal("Pago " + json.RootElement.GetProperty("numero").GetString(), movimiento.Concepto);
            Assert.Null(movimiento.OrdenDePagoId);
        });
    }

    [Fact]
    public async Task Fletero_PagoTotalConOrigenYCeroCaja()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var hoy = new DateOnly(2026, 10, 18);
        var factura = await app.ConAlcanceAsync(async db =>
        {
            var transportista = EscenarioProveedores.Externo("20" + Random.Shared.NextInt64(100000000, 999999999));
            var proveedor = EscenarioProveedores.Fletero(transportista);
            var liquidacion = EscenarioProveedores.Pendiente(transportista, 121m);
            var f = new FacturaProveedor { Proveedor = proveedor, TipoProveedorAlRegistrar = TipoProveedor.Fletero,
                ProveedorRazonSocial = transportista.Nombre, ProveedorCuit = transportista.Cuit,
                Numero = Guid.NewGuid().ToString("N"), Fecha = hoy, Detalle = "Flete", Neto = 100m, Iva = 21m,
                ImporteTotal = 121m, Cae = "1", FechaVencimientoCae = hoy, Liquidacion = liquidacion, ImporteLiquidacionAlRegistrar = 121m };
            db.Add(f);
            db.CambiosDeLiquidacion.Add(new() { LiquidacionId = liquidacion.Id, Liquidacion = liquidacion, Operacion = OperacionDeLiquidacion.Generacion,
                UsuarioId = s.UsuarioId, OcurridoEn = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc) });
            await db.SaveChangesAsync();
            return f;
        });
        using var scope = app.Services.CreateScope();
        var resultado = await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().GenerarAsync(
            new(Guid.NewGuid(), factura.ProveedorId, hoy.AddDays(-1), null, [factura.Id],
                [new ChequeEntrada(Guid.NewGuid(), 121m, s.Banco.Id, "F-001", hoy, hoy.AddDays(30), null)], 121m, true),
            s.UsuarioId, true, hoy, DateTime.UtcNow);
        using var json = JsonDocument.Parse(resultado.Json);
        var id = json.RootElement.GetProperty("id").GetInt32();
        await app.EnLaBaseAsync(async db =>
        {
            var pago = Assert.Single(await db.OrdenesDePago.Where(x => x.OrdenPagoProveedorId == id).ToListAsync());
            Assert.Equal(factura.LiquidacionId, pago.LiquidacionId);
            Assert.Equal(hoy, pago.FechaPago);
            Assert.True(pago.Vigente);
            Assert.Equal(EstadoLiquidacion.Pagada, (await db.Liquidaciones.SingleAsync(x => x.Id == pago.LiquidacionId)).Estado);
            Assert.Empty(await db.MovimientosDeCaja.Where(x => x.OrdenPagoProveedorId == id).ToListAsync());
        });
    }

}
