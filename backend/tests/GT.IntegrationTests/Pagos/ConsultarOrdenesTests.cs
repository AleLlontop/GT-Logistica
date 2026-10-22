using System.Text.Json;
using GT.Application.Pagos;
using GT.Domain.Liquidaciones;
using GT.Domain.Pagos;
using GT.Domain.Proveedores;
using GT.IntegrationTests.Caja;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Proveedores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

public class ConsultarOrdenesTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private static readonly DateOnly Hoy = new(2026, 10, 18);
    private static readonly DateTime Ahora = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);
    private async Task<ResultadoEscrituraPago> Generar(GenerarOrdenEntrada entrada, int usuario)
    {
        using var scope = app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().GenerarAsync(entrada, usuario, true, Hoy, Ahora);
    }
    [Fact]
    public async Task Mixta_DetalleHistoricoCompletoYEstadoActual_ImportesExactos()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var usuario = await app.UsuarioAsync();
        await app.CrearCajaAsync(usuario.Id, saldoInicial: 150m);
        await app.EnLaBaseAsync(db => db.FacturasProveedor.Where(f => f.Id == s.Factura.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(f => f.Neto, 826.45m).SetProperty(f => f.Iva, 173.55m).SetProperty(f => f.ImporteTotal, 1000m)));
        var resultado = await Generar(new(Guid.NewGuid(), s.Orden.ProveedorId, Hoy.AddDays(-1), "Detalle propio", [s.Factura.Id],
            [new ChequeEntrada(Guid.NewGuid(), 600m, s.Banco.Id, "001-123", Hoy.AddDays(-1), Hoy, "Detalle de cheque"),
             new TransferenciaEntrada(Guid.NewGuid(), 250m, Hoy.AddDays(-2), new string('0',22), new string('1',22), "C-001", "Observaciones"),
             new EfectivoEntrada(Guid.NewGuid(), 150m)], 1000m, true), usuario.Id);
        await app.EnLaBaseAsync(async db =>
        {
            await db.Proveedores.Where(p => p.Id == s.Orden.ProveedorId).ExecuteUpdateAsync(u => u.SetProperty(p => p.RazonSocial, "Nombre actual distinto").SetProperty(p => p.Activo, false));
            await db.Bancos.Where(b => b.Id == s.Banco.Id).ExecuteUpdateAsync(u => u.SetProperty(b => b.Nombre, "Banco renombrado"));
        });
        using var cliente = await app.CrearClienteAutenticadoAsync();
        using var json = JsonDocument.Parse(await cliente.GetStringAsync(resultado.Location));
        var orden = json.RootElement;
        Assert.Equal(s.Orden.ProveedorRazonSocial, orden.GetProperty("proveedor").GetProperty("razonSocial").GetString());
        Assert.False(orden.GetProperty("proveedor").GetProperty("activoActual").GetBoolean());
        Assert.True(orden.GetProperty("proveedor").GetProperty("activoAlGenerar").GetBoolean());
        Assert.Equal("2026-10-17", orden.GetProperty("fecha").GetString());
        Assert.Equal("2026-10-18T12:00:00Z", orden.GetProperty("registradaEn").GetString());
        Assert.Equal("1000.00", orden.GetProperty("importeTotal").GetString());
        Assert.Equal("pagada", Assert.Single(orden.GetProperty("facturas").EnumerateArray()).GetProperty("estadoActual").GetString());
        var medios = orden.GetProperty("medios").EnumerateArray().ToArray();
        var cheque = Assert.Single(medios, m => m.GetProperty("tipo").GetString() == "cheque");
        Assert.Equal("Banco renombrado", cheque.GetProperty("banco").GetProperty("nombre").GetString());
        Assert.Equal("Detalle de cheque", cheque.GetProperty("detalle").GetString());
        var transferencia = Assert.Single(medios, m => m.GetProperty("tipo").GetString() == "transferencia");
        Assert.Equal(new string('0',22), transferencia.GetProperty("cbuOrigen").GetString());
        Assert.Equal("Observaciones", transferencia.GetProperty("observaciones").GetString());
        var egreso = orden.GetProperty("caja").GetProperty("egreso");
        Assert.Equal("150.00", egreso.GetProperty("importe").GetString());
        Assert.Equal(orden.GetProperty("numero").GetString(), egreso.GetProperty("ordenPagoProveedor").GetProperty("numero").GetString());
        Assert.Equal("Pago " + orden.GetProperty("numero").GetString(), egreso.GetProperty("concepto").GetString());
    }

    [Fact]
    public async Task FleteroVariasLiquidaciones_UnaOPPorCadaUna_OrigenInequivocoSinDobleImporte()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var facturas = await app.ConAlcanceAsync(async db =>
        {
            var t = EscenarioProveedores.Externo("20" + Random.Shared.NextInt64(100000000,999999999)); var p = EscenarioProveedores.Fletero(t);
            var lista = new List<FacturaProveedor>();
            for (var i = 0; i < 3; i++)
            {
                var l = EscenarioProveedores.Pendiente(t, 121m);
                var f = new FacturaProveedor { Proveedor = p, TipoProveedorAlRegistrar = TipoProveedor.Fletero,
                    ProveedorRazonSocial = t.Nombre, ProveedorCuit = t.Cuit, Numero = Guid.NewGuid().ToString("N"), Fecha = Hoy,
                    Detalle = "Flete", Neto = 100m, Iva = 21m, ImporteTotal = 121m, Cae = "1", FechaVencimientoCae = Hoy,
                    Liquidacion = l, ImporteLiquidacionAlRegistrar = 121m };
                db.Add(f); db.CambiosDeLiquidacion.Add(new() { LiquidacionId = 0, Liquidacion = l, Operacion = OperacionDeLiquidacion.Generacion,
                    UsuarioId = s.UsuarioId, OcurridoEn = Ahora.AddDays(-1) }); lista.Add(f);
            }
            await db.SaveChangesAsync(); return lista;
        });
        var resultado = await Generar(new(Guid.NewGuid(), facturas[0].ProveedorId, Hoy, null, facturas.Select(f => f.Id).ToArray(),
            [new ChequeEntrada(Guid.NewGuid(), 363m, s.Banco.Id, "F-001", Hoy, Hoy.AddDays(30), null)], 363m, true), s.UsuarioId);
        using var cliente = await app.CrearClienteAutenticadoAsync();
        using var json = JsonDocument.Parse(await cliente.GetStringAsync(resultado.Location));
        var orden = json.RootElement;
        Assert.Equal("363.00", orden.GetProperty("importeTotal").GetString());
        Assert.StartsWith("OPP-", orden.GetProperty("numero").GetString());
        var liquidaciones = orden.GetProperty("facturas").EnumerateArray().Select(f => f.GetProperty("liquidacion")).ToArray();
        Assert.Equal(3, liquidaciones.Select(l => l.GetProperty("pago").GetProperty("id").GetInt32()).Distinct().Count());
        Assert.All(liquidaciones, l =>
        {
            Assert.StartsWith("OP-", l.GetProperty("pago").GetProperty("numero").GetString());
            Assert.Equal("121.00", l.GetProperty("pago").GetProperty("importe").GetString());
            Assert.Equal("0.00", l.GetProperty("saldoActual").GetString());
            Assert.Equal("pagada", l.GetProperty("estadoActual").GetString());
        });
        Assert.Equal(JsonValueKind.Null, orden.GetProperty("caja").ValueKind);
    }

    [Fact]
    public async Task FiltrosInclusivosPaginadosConEmpatesYAnuladasHistoricas()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        await app.EnLaBaseAsync(async db =>
        {
            for (var i = 0; i < 22; i++) db.OrdenesPagoProveedor.Add(new()
            {
                ProveedorId = s.Orden.ProveedorId, Fecha = i == 20 ? Hoy.AddDays(-1) : i == 21 ? Hoy.AddDays(1) : Hoy,
                ProveedorRazonSocial = s.Orden.ProveedorRazonSocial, ProveedorCuit = s.Orden.ProveedorCuit,
                TipoProveedorAlRegistrar = s.Orden.TipoProveedorAlRegistrar, ProveedorActivoAlGenerar = true,
                ImporteTotal = 121m, UsuarioId = s.UsuarioId, RegistradaEn = Ahora,
            });
            await db.SaveChangesAsync();
        });
        using var cliente = await app.CrearClienteAutenticadoAsync();
        var ruta = $"/api/pagos/ordenes?proveedorId={s.Orden.ProveedorId}&desde=2026-10-18&hasta=2026-10-18&estado=pendiente";
        using var a = JsonDocument.Parse(await cliente.GetStringAsync(ruta));
        using var b = JsonDocument.Parse(await cliente.GetStringAsync(ruta + "&pagina=2"));
        Assert.Equal(21, a.RootElement.GetProperty("total").GetInt32());
        var ids = a.RootElement.GetProperty("items").EnumerateArray().Concat(b.RootElement.GetProperty("items").EnumerateArray()).Select(o => o.GetProperty("id").GetInt32()).ToArray();
        Assert.Equal(21, ids.Distinct().Count()); Assert.Equal(ids.OrderDescending(), ids);
        await app.EnLaBaseAsync(db => db.OrdenesPagoProveedor.Where(o => o.Id == s.Orden.Id).ExecuteUpdateAsync(u => u
            .SetProperty(o => o.Estado, EstadoOrdenPagoProveedor.Anulada).SetProperty(o => o.MotivoAnulacion, "Historia")
            .SetProperty(o => o.AnuladaPorId, s.UsuarioId).SetProperty(o => o.AnuladaEn, Ahora)));
        using var anuladas = JsonDocument.Parse(await cliente.GetStringAsync($"/api/pagos/ordenes?proveedorId={s.Orden.ProveedorId}&estado=anulada"));
        Assert.Single(anuladas.RootElement.GetProperty("items").EnumerateArray());
        using var detalle = JsonDocument.Parse(await cliente.GetStringAsync($"/api/pagos/ordenes/{s.Orden.Id}"));
        Assert.Equal("Historia", detalle.RootElement.GetProperty("anulacion").GetProperty("motivo").GetString());
        using var vacio = JsonDocument.Parse(await cliente.GetStringAsync($"/api/pagos/ordenes?proveedorId={s.Orden.ProveedorId}&desde=2027-01-01"));
        Assert.Empty(vacio.RootElement.GetProperty("items").EnumerateArray());
    }
}
