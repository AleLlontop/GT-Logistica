using GT.Application.Pagos;
using GT.Domain.Pagos;
using GT.Domain.Proveedores;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

public class CicloPagoReversionTests
{
    private static GenerarOrdenEntrada NuevaIntencion(GenerarOrdenEntrada original) => original with
    {
        ClaveOperacion = Guid.NewGuid(),
        Medios = original.Medios.Select(m => m is ChequeEntrada c ? (MedioEntrada)(c with { RenglonId = Guid.NewGuid(), NumeroCheque = Guid.NewGuid().ToString() }) : m).ToArray(),
    };
    private static async Task<ResultadoEscrituraPago> Generar(AplicacionDePrueba app, PagoParaAnular s, GenerarOrdenEntrada entrada)
    {
        using var scope = app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().GenerarAsync(entrada, s.UsuarioId, true, EscenarioAnulacion.Hoy, EscenarioAnulacion.Ahora);
    }
    [Theory]
    [InlineData("cheque")]
    [InlineData("fletero")]
    public async Task CuatroCiclosConHistoriaConservada_YUnaSolaAsociacionVigente(string tipo)
    {
        var app = new AplicacionDePrueba();
        try
        {
            await app.InitializeAsync(); var s = await EscenarioAnulacion.CrearAsync(app, tipo);
            var id = s.OrdenId;
            for (var ciclo = 0; ciclo < 4; ciclo++)
            {
                using (var scope = app.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().AnularAsync(id,
                    new(Guid.NewGuid(), "Ciclo " + ciclo, true), s.UsuarioId, true, EscenarioAnulacion.Hoy, EscenarioAnulacion.Ahora.AddMinutes(ciclo + 1));
                using (var scope = app.Services.CreateScope()) Assert.Equal(s.Facturas.Length,
                    (await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().SeleccionAsync(s.Facturas)).Items.Count);
                var nuevo = await Generar(app, s, NuevaIntencion(s.Generar));
                using var json = System.Text.Json.JsonDocument.Parse(nuevo.Json); id = json.RootElement.GetProperty("id").GetInt32();
            }
            Assert.Equal(s.JsonOriginal, (await Generar(app, s, s.Generar)).Json);
            Assert.Equal(409, (await Assert.ThrowsAsync<ReglaPagoException>(() => Generar(app, s, NuevaIntencion(s.Generar)))).EstadoHttp);
            await app.EnLaBaseAsync(async db =>
            {
                var vinculos = await db.OrdenPagoProveedorFacturas.Where(v => s.Facturas.Contains(v.FacturaProveedorId)).ToListAsync();
                Assert.Equal(s.Facturas.Length * 5, vinculos.Count);
                foreach (var factura in s.Facturas) Assert.Single(vinculos, v => v.FacturaProveedorId == factura && v.Vigente);
                var ordenes = vinculos.Select(v => v.OrdenPagoProveedorId).Distinct().ToArray();
                Assert.Equal(4, await db.OrdenesPagoProveedor.CountAsync(o => ordenes.Contains(o.Id) && o.Estado == EstadoOrdenPagoProveedor.Anulada));
                Assert.All(await db.FacturasProveedor.Where(f => s.Facturas.Contains(f.Id)).ToListAsync(), f => Assert.Equal(EstadoFacturaProveedor.Pagada, f.Estado));
                if (tipo == "fletero")
                {
                    var pagos = await db.OrdenesDePago.Where(p => s.Liquidaciones.Contains(p.LiquidacionId)).ToListAsync();
                    Assert.Equal(15, pagos.Count); Assert.Equal(3, pagos.Count(p => p.Vigente));
                    foreach (var l in await db.Liquidaciones.Where(l => s.Liquidaciones.Contains(l.Id)).ToListAsync())
                        Assert.Equal(l.ImportePagado, pagos.Where(p => p.LiquidacionId == l.Id && p.Vigente).Sum(p => p.Importe));
                }
            });
        }
        finally { await app.DisposeAsync(); }
    }
    [Fact]
    public async Task DespuesDeAnular_DosNuevasIntencionesNoDejanDosOrdenesEfectivas()
    {
        var app = new ConcurrenciaGenerarTests.AplicacionConBarrera("sp_getapplock");
        try
        {
            await app.InitializeAsync(); var s = await EscenarioAnulacion.CrearAsync(app, "fletero");
            using (var scope = app.Services.CreateScope()) await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().AnularAsync(s.OrdenId,
                new(Guid.NewGuid(), "Liberar asociación", true), s.UsuarioId, true, EscenarioAnulacion.Hoy, EscenarioAnulacion.Ahora.AddMinutes(1));
            async Task<int> Intentar()
            {
                try { return (await Generar(app, s, NuevaIntencion(s.Generar))).EstadoHttp; }
                catch (ReglaPagoException e) { return e.EstadoHttp; }
            }
            app.Barrera.Activa = true;
            var resultados = await Task.WhenAll(Intentar(), Intentar()).WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(new[] { 201, 409 }, resultados.Order().ToArray()); Assert.Equal(2, app.Barrera.Llegadas);
            await app.EnLaBaseAsync(async db =>
            {
                Assert.Equal(6, await db.OrdenPagoProveedorFacturas.CountAsync(v => s.Facturas.Contains(v.FacturaProveedorId)));
                Assert.Equal(3, await db.OrdenPagoProveedorFacturas.CountAsync(v => s.Facturas.Contains(v.FacturaProveedorId) && v.Vigente));
                Assert.Equal(3, await db.OrdenesDePago.CountAsync(p => s.Liquidaciones.Contains(p.LiquidacionId) && p.Vigente));
            });
        }
        finally { await app.DisposeAsync(); }
    }
}
