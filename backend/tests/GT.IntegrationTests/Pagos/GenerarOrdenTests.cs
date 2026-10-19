using System.Text.Json;
using GT.Application.Pagos;
using GT.Domain.Caja;
using GT.Domain.Liquidaciones;
using GT.Domain.Pagos;
using GT.Domain.Proveedores;
using GT.IntegrationTests.Caja;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Proveedores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

public class GenerarOrdenTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private static readonly DateOnly Hoy = new(2026, 10, 18);
    private static readonly DateTime Ahora = new(2026, 10, 19, 1, 0, 0, DateTimeKind.Utc);
    private async Task<ResultadoEscrituraPago> Generar(GenerarOrdenEntrada entrada, int usuario)
    {
        using var scope = app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().GenerarAsync(entrada, usuario, true, Hoy, Ahora);
    }
    private Task CambiarTotal(int factura, decimal total)
    {
        // Datos fiscales válidos de 013: el recorrido Pagos nunca recalcula IVA.
        var neto = decimal.Round(total / 1.21m, 2, MidpointRounding.AwayFromZero);
        var iva = decimal.Round(neto * 0.21m, 2, MidpointRounding.AwayFromZero);
        Assert.Equal(total, neto + iva);
        return app.EnLaBaseAsync(db => db.FacturasProveedor.Where(x => x.Id == factura)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Neto, neto).SetProperty(x => x.Iva, iva).SetProperty(x => x.ImporteTotal, total)));
    }

    [Fact]
    public async Task Mixta_InactivoRetroactiva_SoloEfectivoSaleDeCaja()
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var usuario = await app.UsuarioAsync();
        await app.CrearCajaAsync(usuario.Id, saldoInicial: 150m);
        await CambiarTotal(s.Factura.Id, 1000m);
        // Baja luego de que la factura ya fue seleccionada: no es motivo de rechazo.
        await app.EnLaBaseAsync(db => db.Proveedores.Where(x => x.Id == s.Orden.ProveedorId)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Activo, false)));
        var entrada = new GenerarOrdenEntrada(Guid.NewGuid(), s.Orden.ProveedorId, Hoy.AddDays(-2), "  Servicios  ", [s.Factura.Id],
            [new ChequeEntrada(Guid.NewGuid(), 600m, s.Banco.Id, "000-001", Hoy, Hoy, null),
             new TransferenciaEntrada(Guid.NewGuid(), 250m, Hoy.AddDays(-1), new string('0', 22), new string('1', 22), "C-001", "Transferencia"),
             new EfectivoEntrada(Guid.NewGuid(), 150m)], 1000m, true);
        var resultado = await Generar(entrada, usuario.Id);
        using var json = JsonDocument.Parse(resultado.Json);
        var r = json.RootElement;
        Assert.False(r.GetProperty("proveedor").GetProperty("activoAlGenerar").GetBoolean());
        Assert.False(r.GetProperty("proveedor").GetProperty("activoActual").GetBoolean());
        Assert.Equal("2026-10-16", r.GetProperty("fecha").GetString());
        Assert.Equal("Servicios", r.GetProperty("detalle").GetString());
        Assert.Equal("150.00", r.GetProperty("caja").GetProperty("egreso").GetProperty("importe").GetString());
        Assert.Equal(3, r.GetProperty("medios").GetArrayLength());
        var fechas = r.GetProperty("medios").EnumerateArray().Select(x => x.GetProperty("fecha").GetString()).ToArray();
        Assert.Equal(2, fechas.Count(x => x == "2026-10-18"));
        Assert.Contains("2026-10-17", fechas);
        await app.EnLaBaseAsync(async db => Assert.Single(await db.MovimientosDeCaja.Where(x => x.OrdenPagoProveedorId == r.GetProperty("id").GetInt32()).ToListAsync()));
    }

    [Theory]
    [InlineData("exacto")]
    [InlineData("insuficiente")]
    [InlineData("sin_caja")]
    [InlineData("ajena")]
    [InlineData("cerrada")]
    public async Task EfectivoConjunto_SaldoYPropiedad(string caso)
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var usuario = await app.UsuarioAsync();
        await CambiarTotal(s.Factura.Id, 110m);
        if (caso is not "sin_caja")
        {
            var responsable = caso == "ajena" ? (await app.UsuarioAsync()).Id : usuario.Id;
            var caja = await app.CrearCajaAsync(responsable, saldoInicial: caso == "insuficiente" ? 100m : 110m);
            if (caso == "cerrada") await app.EnLaBaseAsync(db => db.Cajas.Where(x => x.Id == caja.Id).ExecuteUpdateAsync(u => u
                .SetProperty(x => x.Estado, EstadoCaja.Cerrada).SetProperty(x => x.FechaCierre, Ahora).SetProperty(x => x.SaldoFinal, 110m)));
        }
        var entrada = new GenerarOrdenEntrada(Guid.NewGuid(), s.Orden.ProveedorId, Hoy, null, [s.Factura.Id],
            [new EfectivoEntrada(Guid.NewGuid(), 60m), new EfectivoEntrada(Guid.NewGuid(), 50m)], 110m, true);
        if (caso == "exacto")
        {
            using var json = JsonDocument.Parse((await Generar(entrada, usuario.Id)).Json);
            Assert.Equal("110.00", json.RootElement.GetProperty("caja").GetProperty("egreso").GetProperty("importe").GetString());
            Assert.Equal(2, json.RootElement.GetProperty("medios").GetArrayLength());
        }
        else
        {
            Assert.Equal(409, (await Assert.ThrowsAsync<ReglaPagoException>(() => Generar(entrada, usuario.Id))).EstadoHttp);
            await SinEfectos(entrada);
        }
    }

    [Theory]
    [InlineData("0.01")]
    [InlineData("9999999999999999.99")]
    public async Task CentavoYMaximoDecimal_SinPerderPrecision(string importe)
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var total = decimal.Parse(importe, System.Globalization.CultureInfo.InvariantCulture);
        await CambiarTotal(s.Factura.Id, total);
        var entrada = new GenerarOrdenEntrada(Guid.NewGuid(), s.Orden.ProveedorId, Hoy, "   ", [s.Factura.Id],
            [new ChequeEntrada(Guid.NewGuid(), total, s.Banco.Id, "Máximo", Hoy, Hoy, null)], total, true);
        using var json = JsonDocument.Parse((await Generar(entrada, s.UsuarioId)).Json);
        Assert.Equal(importe, json.RootElement.GetProperty("importeTotal").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("detalle").ValueKind);
    }

    [Theory]
    [InlineData("emision_futura")]
    [InlineData("fecha_opp_futura")]
    [InlineData("fecha_opp_ausente")]
    [InlineData("centavo")]
    [InlineData("numero_largo")]
    [InlineData("banco_inactivo")]
    public async Task EntradaInvalida_NoPersisteNingunEfecto(string caso)
    {
        var s = await EscenarioPagos.CrearAsync(app);
        if (caso == "banco_inactivo") await app.EnLaBaseAsync(db => db.Bancos.Where(x => x.Id == s.Banco.Id)
            .ExecuteUpdateAsync(u => u.SetProperty(x => x.Activo, false)));
        var cheque = new ChequeEntrada(Guid.NewGuid(), caso == "centavo" ? 120.99m : 121m, s.Banco.Id,
            caso == "numero_largo" ? new string('1', 201) : "000-001", caso == "emision_futura" ? Hoy.AddDays(1) : Hoy, Hoy.AddDays(2), null);
        var entrada = new GenerarOrdenEntrada(Guid.NewGuid(), s.Orden.ProveedorId,
            caso == "fecha_opp_ausente" ? null : caso == "fecha_opp_futura" ? Hoy.AddDays(1) : Hoy,
            null, [s.Factura.Id], [cheque], 121m, true);
        var error = await Assert.ThrowsAsync<ReglaPagoException>(() => Generar(entrada, s.UsuarioId));
        Assert.Equal(caso == "banco_inactivo" ? 409 : 400, error.EstadoHttp);
        if (caso == "numero_largo") { Assert.Equal("longitud_excedida", error.Codigo); Assert.Equal("medios[0].numeroCheque", error.Campo); }
        await SinEfectos(entrada);
    }


    [Theory]
    [InlineData("valida")]
    [InlineData("saldo_parcial")]
    [InlineData("total_cambio")]
    [InlineData("pagada")]
    [InlineData("anulada")]
    [InlineData("efectivo")]
    [InlineData("transferencia")]
    [InlineData("no_diferido")]
    public async Task FleteroVariasFacturas_RechazoCompletoSiUnaNoCumple(string caso)
    {
        var s = await EscenarioPagos.CrearAsync(app);
        var facturas = await app.ConAlcanceAsync(async db =>
        {
            var transportista = EscenarioProveedores.Externo("20" + Random.Shared.NextInt64(100000000, 999999999));
            var proveedor = EscenarioProveedores.Fletero(transportista);
            proveedor.Activo = false;
            var lista = new List<FacturaProveedor>();
            for (var i = 0; i < 3; i++)
            {
                var liquidacion = EscenarioProveedores.Pendiente(transportista, 121m);
                var f = new FacturaProveedor { Proveedor = proveedor, TipoProveedorAlRegistrar = TipoProveedor.Fletero,
                    ProveedorRazonSocial = transportista.Nombre, ProveedorCuit = transportista.Cuit,
                    Numero = Guid.NewGuid().ToString("N"), Fecha = Hoy, Detalle = "Flete", Neto = 100m, Iva = 21m,
                    ImporteTotal = 121m, Cae = "1", FechaVencimientoCae = Hoy, Liquidacion = liquidacion, ImporteLiquidacionAlRegistrar = 121m };
                if (i == 2)
                {
                    if (caso == "saldo_parcial") liquidacion.ImportePagado = 1m;
                    if (caso == "total_cambio") liquidacion.ImporteTotal = 122m;
                    if (caso == "pagada") { liquidacion.Estado = EstadoLiquidacion.Pagada; liquidacion.ImportePagado = 121m; }
                    if (caso == "anulada") { liquidacion.Estado = EstadoLiquidacion.Anulada; liquidacion.MotivoAnulacion = "Prueba"; }
                }
                db.Add(f);
                if (f.Liquidacion is not null) db.CambiosDeLiquidacion.Add(new() { LiquidacionId = 0, Liquidacion = liquidacion,
                    Operacion = OperacionDeLiquidacion.Generacion, UsuarioId = s.UsuarioId, OcurridoEn = Ahora.AddDays(-2) });
                lista.Add(f);
            }
            await db.SaveChangesAsync();
            return lista;
        });
        var medios = new List<MedioEntrada> { new ChequeEntrada(Guid.NewGuid(), caso is "efectivo" or "transferencia" ? 362m : 363m,
            s.Banco.Id, "001-000123", Hoy, caso == "no_diferido" ? Hoy : Hoy.AddDays(30), null) };
        if (caso == "efectivo") medios.Add(new EfectivoEntrada(Guid.NewGuid(), 1m));
        if (caso == "transferencia") medios.Add(new TransferenciaEntrada(Guid.NewGuid(), 1m, Hoy, new string('0',22), new string('1',22), "1", null));
        var entrada = new GenerarOrdenEntrada(Guid.NewGuid(), facturas[0].ProveedorId, Hoy, null,
            facturas.Select(x => x.Id).ToArray(), medios.ToArray(), 363m, true);
        if (caso == "valida")
        {
            using var json = JsonDocument.Parse((await Generar(entrada, s.UsuarioId)).Json);
            var id = json.RootElement.GetProperty("id").GetInt32();
            Assert.Equal(3, json.RootElement.GetProperty("facturas").GetArrayLength());
            await app.EnLaBaseAsync(async db =>
            {
                var pagos = await db.OrdenesDePago.Where(x => x.OrdenPagoProveedorId == id).ToListAsync();
                Assert.Equal(3, pagos.Count);
                Assert.Equal(3, pagos.Select(x => x.Id).Distinct().Count());
                Assert.All(pagos, x => { Assert.Equal(121m, x.Importe); Assert.Equal(Hoy, x.FechaPago); });
                Assert.Empty(await db.MovimientosDeCaja.Where(x => x.OrdenPagoProveedorId == id).ToListAsync());
            });
        }
        else
        {
            await Assert.ThrowsAsync<ReglaPagoException>(() => Generar(entrada, s.UsuarioId));
            await SinEfectos(entrada);
            await app.EnLaBaseAsync(async db => Assert.Empty(await db.OrdenesDePago.Where(x => facturas.Select(f => f.LiquidacionId).Contains(x.LiquidacionId)).ToListAsync()));
        }
    }

    private Task SinEfectos(GenerarOrdenEntrada entrada) => app.EnLaBaseAsync(async db =>
    {
        Assert.Empty(await db.ResultadosOperacionPago.Where(x => x.Clave == entrada.ClaveOperacion).ToListAsync());
        Assert.Empty(await db.OrdenPagoProveedorFacturas.Where(x => entrada.FacturaIds.Contains(x.FacturaProveedorId)).ToListAsync());
        Assert.All(await db.FacturasProveedor.Where(x => entrada.FacturaIds.Contains(x.Id)).ToListAsync(), f => Assert.Equal(EstadoFacturaProveedor.Pendiente, f.Estado));
    });
}
