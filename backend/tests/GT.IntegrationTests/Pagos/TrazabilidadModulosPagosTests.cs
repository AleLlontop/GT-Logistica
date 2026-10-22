using System.Text.Json;
using GT.Application.Pagos;
using GT.Domain.Liquidaciones;
using GT.Domain.Proveedores;
using GT.IntegrationTests.Caja;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Proveedores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

public class TrazabilidadModulosPagosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private static readonly DateOnly Hoy = new(2026, 10, 18);
    private static readonly DateTime Ahora = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);
    [Fact]
    public async Task LiquidacionExponeOrigenOPPYEvento_SinCambiarMonedaLegacy()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var factura = await app.ConAlcanceAsync(async db =>
        {
            var t = EscenarioProveedores.Externo("20" + Random.Shared.NextInt64(100000000, 999999999));
            var p = EscenarioProveedores.Fletero(t); var l = EscenarioProveedores.Pendiente(t, 121m);
            var f = new FacturaProveedor { Proveedor = p, TipoProveedorAlRegistrar = TipoProveedor.Fletero,
                ProveedorRazonSocial = t.Nombre, ProveedorCuit = t.Cuit, Numero = Guid.NewGuid().ToString("N"), Fecha = Hoy,
                Detalle = "Flete", Neto = 100m, Iva = 21m, ImporteTotal = 121m, Cae = "1", FechaVencimientoCae = Hoy,
                Liquidacion = l, ImporteLiquidacionAlRegistrar = 121m };
            db.Add(f); db.CambiosDeLiquidacion.Add(new() { LiquidacionId = 0, Liquidacion = l,
                Operacion = OperacionDeLiquidacion.Generacion, UsuarioId = s.UsuarioId, OcurridoEn = Ahora.AddDays(-1) });
            await db.SaveChangesAsync(); return f;
        });
        using var scope = app.Services.CreateScope();
        var resultado = await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().GenerarAsync(
            new(Guid.NewGuid(), factura.ProveedorId, Hoy, null, [factura.Id],
                [new ChequeEntrada(Guid.NewGuid(), 121m, s.Banco.Id, "000-001", Hoy, Hoy.AddDays(30), null)], 121m, true), s.UsuarioId, true, Hoy, Ahora);
        using var opp = JsonDocument.Parse(resultado.Json);
        using var cliente = await app.CrearClienteAutenticadoAsync();
        using var detalle = JsonDocument.Parse(await cliente.GetStringAsync($"/api/liquidaciones/{factura.LiquidacionId}"));
        var pago = Assert.Single(detalle.RootElement.GetProperty("ordenesDePago").EnumerateArray());
        Assert.Equal(JsonValueKind.Number, pago.GetProperty("importe").ValueKind);
        Assert.Equal(121m, pago.GetProperty("importe").GetDecimal());
        Assert.True(pago.GetProperty("vigente").GetBoolean());
        Assert.StartsWith("OP-", pago.GetProperty("numero").GetString());
        Assert.Equal(opp.RootElement.GetProperty("numero").GetString(), pago.GetProperty("ordenPagoProveedor").GetProperty("numero").GetString());
        var evento = Assert.Single(detalle.RootElement.GetProperty("historial").EnumerateArray(), e => e.GetProperty("operacion").GetString() == "pagoPorOPP");
        Assert.Equal(pago.GetProperty("numero").GetString(), evento.GetProperty("pago").GetProperty("numero").GetString());
        Assert.Equal(opp.RootElement.GetProperty("id").GetInt32(), evento.GetProperty("ordenPagoProveedor").GetProperty("id").GetInt32());
        using var facturaLeida = JsonDocument.Parse(await cliente.GetStringAsync($"/api/proveedores/facturas/{factura.Id}"));
        Assert.Equal("pagada", facturaLeida.RootElement.GetProperty("estado").GetString());
        Assert.Equal(JsonValueKind.String, facturaLeida.RootElement.GetProperty("importeTotal").ValueKind);
    }

    [Fact]
    public async Task MovimientoCajaExponeOPPSeparada_YConservaImporteNumerico()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        await app.CrearCajaAsync(s.UsuarioId, saldoInicial: 121m);
        using var scope = app.Services.CreateScope();
        var resultado = await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().GenerarAsync(
            new(Guid.NewGuid(), s.Orden.ProveedorId, Hoy, null, [s.Factura.Id], [new EfectivoEntrada(Guid.NewGuid(), 121m)], 121m, true),
            s.UsuarioId, true, Hoy, Ahora);
        using var opp = JsonDocument.Parse(resultado.Json);
        var movimientoId = opp.RootElement.GetProperty("caja").GetProperty("egreso").GetProperty("id").GetInt32();
        using var cliente = await app.CrearClienteAutenticadoAsync();
        var cajaId = opp.RootElement.GetProperty("caja").GetProperty("id").GetInt32();
        await app.EnLaBaseAsync(async db => Assert.True(await db.Cajas.AnyAsync(x => x.Id == cajaId), $"Caja de DTO inexistente: {cajaId}"));
        using var respuesta = await cliente.GetAsync($"/api/caja/{cajaId}/movimientos");
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.True(respuesta.IsSuccessStatusCode, $"GET caja/{cajaId}/movimientos: {(int)respuesta.StatusCode} {cuerpo}");
        using var listado = JsonDocument.Parse(cuerpo);
        var movimiento = Assert.Single(listado.RootElement.GetProperty("items").EnumerateArray(), m => m.GetProperty("id").GetInt32() == movimientoId);
        Assert.Equal(JsonValueKind.Number, movimiento.GetProperty("importe").ValueKind);
        Assert.Equal(opp.RootElement.GetProperty("numero").GetString(), movimiento.GetProperty("referencia").GetString());
        Assert.Equal(opp.RootElement.GetProperty("id").GetInt32(), movimiento.GetProperty("ordenPagoProveedor").GetProperty("id").GetInt32());
        await app.EnLaBaseAsync(async db => Assert.Null((await db.MovimientosDeCaja.SingleAsync(x => x.Id == movimientoId)).OrdenDePagoId));
    }
}
