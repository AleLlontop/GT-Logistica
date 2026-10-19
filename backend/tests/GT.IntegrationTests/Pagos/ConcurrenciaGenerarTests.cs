using System.Data.Common;
using GT.Application.Caja;
using GT.Application.Liquidaciones;
using GT.Application.Pagos;
using GT.Domain.Caja;
using GT.Domain.Liquidaciones;
using GT.Domain.Pagos;
using GT.Domain.Proveedores;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Caja;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Proveedores;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

public class ConcurrenciaGenerarTests
{
    private static readonly DateOnly Hoy = new(2026, 10, 18);
    private static readonly DateTime Ahora = new(2026, 10, 18, 12, 0, 0, DateTimeKind.Utc);
    private static GenerarOrdenEntrada Entrada(int proveedor, int[] facturas, int banco, string numero, bool efectivo = false) =>
        new(Guid.NewGuid(), proveedor, Hoy, null, facturas,
            efectivo ? [new EfectivoEntrada(Guid.NewGuid(), 121m * facturas.Length)]
                : [new ChequeEntrada(Guid.NewGuid(), 121m * facturas.Length, banco, numero, Hoy, Hoy.AddDays(1), null)],
            121m * facturas.Length, true);
    private static async Task<int> Generar(AplicacionDePrueba app, GenerarOrdenEntrada entrada, int usuario)
    {
        using var scope = app.Services.CreateScope();
        try { return (await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().GenerarAsync(entrada, usuario, true, Hoy, Ahora)).EstadoHttp; }
        catch (ReglaPagoException e) { return e.EstadoHttp; }
    }
    private static Task<FacturaProveedor> OtraFactura(AplicacionDePrueba app, FacturaProveedor modelo) => app.ConAlcanceAsync(async db =>
    {
        var f = new FacturaProveedor { ProveedorId = modelo.ProveedorId, TipoProveedorAlRegistrar = modelo.TipoProveedorAlRegistrar,
            ProveedorRazonSocial = modelo.ProveedorRazonSocial, ProveedorCuit = modelo.ProveedorCuit,
            Numero = Guid.NewGuid().ToString("N"), Fecha = Hoy, Detalle = "Otra factura", Neto = 100m, Iva = 21m,
            ImporteTotal = 121m, Cae = "1", FechaVencimientoCae = Hoy };
        db.Add(f); await db.SaveChangesAsync(); return f;
    });

    [Theory]
    [InlineData("misma_factura")]
    [InlineData("superpuestas")]
    [InlineData("cheque_duplicado")]
    [InlineData("bancos_distintos")]
    [InlineData("saldo")]
    public async Task DosIntenciones_RestriccionesPersistidasSinOperacionParcial(string caso)
    {
        var app = new AplicacionConBarrera("sp_getapplock");
        try
        {
            await app.InitializeAsync();
            var a = await EscenarioPagos.CrearAsync(app);
            var b = await OtraFactura(app, a.Factura);
            var c = await OtraFactura(app, a.Factura);
            var usuario = await app.UsuarioAsync();
            if (caso == "saldo") await app.CrearCajaAsync(usuario.Id, saldoInicial: 121m);
            var bancoB = caso == "bancos_distintos" ? (await EscenarioPagos.CrearAsync(app)).Banco.Id : a.Banco.Id;
            var idsA = caso == "superpuestas" ? new[] { a.Factura.Id, b.Id } : [a.Factura.Id];
            var idsB = caso == "misma_factura" ? new[] { a.Factura.Id } : caso == "superpuestas" ? [b.Id, c.Id] : [b.Id];
            var e1 = Entrada(a.Orden.ProveedorId, idsA, a.Banco.Id, caso is "cheque_duplicado" or "bancos_distintos" ? "000123" : "A", caso == "saldo");
            var e2 = Entrada(a.Orden.ProveedorId, idsB, bancoB, caso is "cheque_duplicado" or "bancos_distintos" ? "  000123  " : "B", caso == "saldo");
            app.Barrera.Activa = true;
            var respuestas = await Task.WhenAll(Generar(app, e1, usuario.Id), Generar(app, e2, usuario.Id)).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(2, app.Barrera.Llegadas);
            Assert.Equal(caso == "bancos_distintos" ? 2 : 1, respuestas.Count(x => x == 201));
            if (caso != "bancos_distintos") Assert.Contains(caso == "cheque_duplicado" ? 400 : 409, respuestas);
            await app.EnLaBaseAsync(async db =>
            {
                var claves = new[] { e1.ClaveOperacion, e2.ClaveOperacion };
                var resultados = await db.ResultadosOperacionPago.Where(x => claves.Contains(x.Clave)).ToListAsync();
                Assert.Equal(respuestas.Count(x => x == 201), resultados.Count);
                var ordenes = resultados.Select(x => x.OrdenPagoProveedorId).ToArray();
                var vinculos = await db.OrdenPagoProveedorFacturas.Where(x => ordenes.Contains(x.OrdenPagoProveedorId)).ToListAsync();
                Assert.Equal(caso == "superpuestas" ? 2 : resultados.Count, vinculos.Count);
                Assert.Equal(vinculos.Count, vinculos.Select(x => x.FacturaProveedorId).Distinct().Count());
                if (caso == "saldo") Assert.Single(await db.MovimientosDeCaja.Where(x => ordenes.Contains(x.OrdenPagoProveedorId ?? 0)).ToListAsync());
            });
        }
        finally { await app.DisposeAsync(); }
    }

    [Theory]
    [InlineData("cierre")]
    [InlineData("movimiento")]
    public async Task CajaManualContraGenerar_RevalidaBajoMismoLock(string caso)
    {
        var app = new AplicacionConBarrera("[Cajas]");
        try
        {
            await app.InitializeAsync();
            var a = await EscenarioPagos.CrearAsync(app);
            var usuario = await app.UsuarioAsync();
            var caja = await app.CrearCajaAsync(usuario.Id, saldoInicial: 121m);
            var e = Entrada(a.Orden.ProveedorId, [a.Factura.Id], a.Banco.Id, "A", true);
            async Task<bool> Manual()
            {
                using var scope = app.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IRepositorioCaja>();
                if (caso == "cierre") return (await repo.CerrarAsync(caja.Id, usuario.Id, 121m, Ahora)).Cerrada;
                return (await repo.RegistrarMovimientoAsync(new() { CajaId = caja.Id, Tipo = TipoMovimientoCaja.Egreso,
                    Importe = 121m, Concepto = "Movimiento manual", UsuarioId = usuario.Id, Fecha = Ahora })).Id.HasValue;
            }
            app.Barrera.Activa = true;
            var generar = Generar(app, e, usuario.Id);
            var manual = Manual();
            await Task.WhenAll(generar, (Task)manual).WaitAsync(TimeSpan.FromSeconds(30));
            var estadoGenerar = await generar;
            var completoManual = await manual;
            Assert.Equal(2, app.Barrera.Llegadas);
            Assert.Contains(estadoGenerar, new[] { 201, 409 });
            if (caso == "cierre") Assert.NotEqual(estadoGenerar == 201, completoManual);
            else Assert.True(completoManual); // 011 conserva que un movimiento manual pueda dejar saldo negativo.
            await app.EnLaBaseAsync(async db =>
            {
                var actual = await db.Cajas.SingleAsync(x => x.Id == caja.Id);
                var movimientos = await db.MovimientosDeCaja.Where(x => x.CajaId == caja.Id).ToListAsync();
                Assert.Equal(estadoGenerar == 201 ? 1 : 0, movimientos.Count(x => x.OrdenPagoProveedorId.HasValue));
                if (actual.Estado == EstadoCaja.Cerrada) Assert.Equal(actual.SaldoInicial - movimientos.Sum(x => x.Importe), actual.SaldoFinal);
            });
        }
        finally { await app.DisposeAsync(); }
    }

    [Fact]
    public async Task DesactivarBancoContraGenerar_NoAceptaBancoQuePerdioElegibilidad()
    {
        var app = new AplicacionConBarrera("[Bancos]");
        try
        {
            await app.InitializeAsync();
            var a = await EscenarioPagos.CrearAsync(app);
            async Task Baja()
            {
                using var scope = app.Services.CreateScope();
                await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().ActivarBancoAsync(a.Banco.Id, false);
            }
            app.Barrera.Activa = true;
            var generar = Generar(app, Entrada(a.Orden.ProveedorId, [a.Factura.Id], a.Banco.Id, "A"), a.UsuarioId);
            await Task.WhenAll(generar, Baja()).WaitAsync(TimeSpan.FromSeconds(30));
            var estadoGenerar = await generar;
            Assert.Contains(estadoGenerar, new[] { 201, 409 });
            Assert.Equal(2, app.Barrera.Llegadas);
            await app.EnLaBaseAsync(async db => Assert.False((await db.Bancos.SingleAsync(x => x.Id == a.Banco.Id)).Activo));
        }
        finally { await app.DisposeAsync(); }
    }


    [Theory]
    [InlineData("pago")]
    [InlineData("edicion")]
    [InlineData("anulacion")]
    public async Task LiquidacionManualContraGenerar_ComparteFilaYNoDuplicaPago(string caso)
    {
        var app = new AplicacionConBarrera("[Liquidaciones]");
        try
        {
            await app.InitializeAsync();
            var a = await EscenarioPagos.CrearAsync(app);
            var f = await app.ConAlcanceAsync(async db =>
            {
                var t = EscenarioProveedores.Externo("20" + Random.Shared.NextInt64(100000000, 999999999));
                var p = EscenarioProveedores.Fletero(t);
                var l = EscenarioProveedores.Pendiente(t, 121m);
                var factura = new FacturaProveedor { Proveedor = p, TipoProveedorAlRegistrar = TipoProveedor.Fletero,
                    ProveedorRazonSocial = t.Nombre, ProveedorCuit = t.Cuit, Numero = Guid.NewGuid().ToString("N"),
                    Fecha = Hoy, Detalle = "Flete", Neto = 100m, Iva = 21m, ImporteTotal = 121m, Cae = "1",
                    FechaVencimientoCae = Hoy, Liquidacion = l, ImporteLiquidacionAlRegistrar = 121m };
                db.Add(factura);
                db.CambiosDeLiquidacion.Add(new() { LiquidacionId = 0, Liquidacion = l, Operacion = OperacionDeLiquidacion.Generacion,
                    UsuarioId = a.UsuarioId, OcurridoEn = Ahora.AddDays(-1) });
                await db.SaveChangesAsync();
                return factura;
            });
            async Task<bool> Manual()
            {
                using var scope = app.Services.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IRepositorioLiquidaciones>();
                return caso switch
                {
                    "pago" => await repo.RegistrarPagoAsync(f.LiquidacionId!.Value, Hoy, 1m, a.UsuarioId, Ahora) is not null,
                    "edicion" => await repo.EditarAsync(f.LiquidacionId!.Value, 0, 121m, [], [], a.UsuarioId, Ahora),
                    _ => await repo.AnularAsync(f.LiquidacionId!.Value, "Anulación manual", a.UsuarioId, Ahora),
                };
            }
            app.Barrera.Activa = true;
            var generar = Generar(app, Entrada(f.ProveedorId, [f.Id], a.Banco.Id, "LQ"), a.UsuarioId);
            var manual = Manual();
            await Task.WhenAll(generar, (Task)manual).WaitAsync(TimeSpan.FromSeconds(30));
            var estadoGenerar = await generar;
            var completoManual = await manual;
            Assert.Equal(2, app.Barrera.Llegadas);
            Assert.Contains(estadoGenerar, new[] { 201, 409 });
            if (caso != "edicion") Assert.NotEqual(estadoGenerar == 201, completoManual);
            else Assert.Equal(201, estadoGenerar); // Editar sin cambiar el total no vuelve inelegible la deuda.
            await app.EnLaBaseAsync(async db =>
            {
                var l = await db.Liquidaciones.SingleAsync(x => x.Id == f.LiquidacionId);
                var pagos = await db.OrdenesDePago.Where(x => x.LiquidacionId == l.Id && x.Vigente).ToListAsync();
                Assert.Equal(pagos.Sum(x => x.Importe), l.ImportePagado);
                if (estadoGenerar == 201) { Assert.Single(pagos); Assert.Equal(121m, l.ImportePagado); Assert.NotNull(pagos[0].OrdenPagoProveedorId); }
                else Assert.Empty(await db.OrdenPagoProveedorFacturas.Where(x => x.FacturaProveedorId == f.Id).ToListAsync());
            });
        }
        finally { await app.DisposeAsync(); }
    }

    internal sealed class AplicacionConBarrera(string objetivo) : AplicacionDePrueba
    {
        public BarreraSql Barrera { get; } = new(objetivo);
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(s => s.AddDbContext<GtDbContext>(o => o.AddInterceptors(Barrera)));
        }
    }
    internal sealed class BarreraSql(string objetivo) : DbCommandInterceptor
    {
        public bool Activa;
        private int llegadas;
        public int Llegadas => Math.Min(2, llegadas);
        private readonly TaskCompletionSource salida = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (Activa && command.CommandText.Contains(objetivo, StringComparison.Ordinal)
                && (objetivo == "sp_getapplock" || command.CommandText.StartsWith("UPDATE", StringComparison.Ordinal)))
            {
                var numero = Interlocked.Increment(ref llegadas);
                if (numero == 2) salida.TrySetResult();
                if (numero <= 2) await salida.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
