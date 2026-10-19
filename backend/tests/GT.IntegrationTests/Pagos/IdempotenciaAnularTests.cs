using GT.Application.Pagos;
using GT.Domain.Caja;
using GT.Domain.Pagos;
using GT.IntegrationTests.Caja;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Pagos;

public class IdempotenciaAnularTests
{
    private static async Task<ResultadoEscrituraPago> Anular(AplicacionDePrueba app, PagoParaAnular s, AnularOrdenEntrada e, int? usuario = null, bool consultar = true)
    {
        using var scope = app.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().AnularAsync(s.OrdenId, e, usuario ?? s.UsuarioId, consultar, EscenarioAnulacion.Hoy, EscenarioAnulacion.Ahora.AddMinutes(1));
    }
    private static Task VerificarUnicaCompensacion(AplicacionDePrueba app, PagoParaAnular s) => app.EnLaBaseAsync(async db =>
    {
        Assert.Single(await db.ResultadosOperacionPago.Where(r => r.OrdenPagoProveedorId == s.OrdenId && r.Operacion == TipoOperacionPago.Anular).ToListAsync());
        if (s.CajaId.HasValue) Assert.Single(await db.MovimientosDeCaja.Where(m => m.OrdenPagoProveedorId == s.OrdenId && m.Tipo == TipoMovimientoCaja.Ingreso).ToListAsync());
        foreach (var c in await db.Cheques.Where(c => c.OrdenPagoProveedorId == s.OrdenId).ToListAsync())
            Assert.Single(await db.HistorialEstadosCheque.Where(h => h.ChequeId == c.Id).ToListAsync());
        foreach (var p in await db.OrdenesDePago.Where(p => p.OrdenPagoProveedorId == s.OrdenId).ToListAsync())
            Assert.Single(await db.CambiosDeLiquidacion.Where(h => h.OrdenDePagoId == p.Id && h.Operacion == GT.Domain.Liquidaciones.OperacionDeLiquidacion.ReversionPagoPorOPP).ToListAsync());
    });

    [Theory]
    [InlineData("efectivo")]
    [InlineData("fletero")]
    public async Task ClaveGlobalReplayConcurrenteYUsuarios_NoDuplicaNiFiltra(string tipo)
    {
        var app = new IdempotenciaGenerarTests.AplicacionIntervenida();
        try
        {
            await app.InitializeAsync(); var s = await EscenarioAnulacion.CrearAsync(app, tipo);
            // El mismo UUID de Generar pertenece al espacio Anular independiente.
            var e = new AnularOrdenEntrada(s.Generar.ClaveOperacion, "Corrección", true);
            app.Comandos.BarreraActiva = true;
            var respuestas = await Task.WhenAll(Anular(app, s, e), Anular(app, s, e)).WaitAsync(TimeSpan.FromSeconds(30));
            app.Comandos.BarreraActiva = false;
            Assert.Equal(respuestas[0], respuestas[1]); Assert.Equal(200, respuestas[0].EstadoHttp);
            var otro = await app.UsuarioAsync();
            Assert.Equal(403, (await Assert.ThrowsAsync<ReglaPagoException>(() => Anular(app, s, e with { Motivo = "Otro" }, otro.Id, false))).EstadoHttp);
            Assert.Equal(respuestas[0], await Anular(app, s, e, otro.Id, true));
            Assert.Equal(409, (await Assert.ThrowsAsync<ReglaPagoException>(() => Anular(app, s, e with { Motivo = "Otro" }))).EstadoHttp);
            Assert.Equal(409, (await Assert.ThrowsAsync<ReglaPagoException>(() => Anular(app, s, e with { ClaveOperacion = Guid.NewGuid() }))).EstadoHttp);
            using var scope = app.Services.CreateScope();
            var replay = await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().GenerarAsync(s.Generar, s.UsuarioId, true,
                EscenarioAnulacion.Hoy.AddDays(1), EscenarioAnulacion.Ahora.AddDays(1));
            Assert.Equal(201, replay.EstadoHttp); Assert.Equal(s.JsonOriginal, replay.Json);
            await VerificarUnicaCompensacion(app, s);
        }
        finally { await app.DisposeAsync(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommitRealPerdido_RecuperaO503_ReintentoMismaClaveNoRecompensa(bool fallaRecuperacion)
    {
        var app = new IdempotenciaGenerarTests.AplicacionIntervenida();
        try
        {
            await app.InitializeAsync(); var s = await EscenarioAnulacion.CrearAsync(app, "efectivo");
            var e = new AnularOrdenEntrada(Guid.NewGuid(), "Motivo", true);
            app.Commit.Activo = true; app.Comandos.FallarRecuperacion = fallaRecuperacion;
            if (fallaRecuperacion) Assert.Equal(503, (await Assert.ThrowsAsync<ReglaPagoException>(() => Anular(app, s, e))).EstadoHttp);
            else Assert.Equal(200, (await Anular(app, s, e)).EstadoHttp);
            Assert.True(app.Commit.Disparado); app.Comandos.FallarRecuperacion = false;
            Assert.Equal(200, (await Anular(app, s, e)).EstadoHttp); await VerificarUnicaCompensacion(app, s);
        }
        finally { await app.DisposeAsync(); }
    }
}
