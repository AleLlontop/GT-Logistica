using GT.Application.Pagos;
using GT.Application.Reportes.Analisis;
using GT.Domain.Pagos;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Liquidaciones;
using GT.IntegrationTests.Pagos;
using GT.IntegrationTests.Viajes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class PagosAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task OpEsUnidad_ReversionPosteriorNoSuma_NuevoPagoNoDuplicaHistoria()
    {
        var pago = await EscenarioAnulacion.CrearAsync(app, "fletero");
        var cliente = await app.CrearClienteAsync();
        var transportista = await app.TransportistaExternoAsync();
        var viaje = await app.ViajeDeAsync(cliente.Id, transportista.Id, importe: 100m, fecha: new(2026, 9, 1));
        var manual = await app.CrearLiquidacionAsync(transportista.Id, [viaje], importePagado: 50m, mes: 9);
        await app.EnLaBaseAsync(db => db.OrdenesDePago.Where(o => o.LiquidacionId == manual.Id).ExecuteUpdateAsync(s => s.SetProperty(o => o.FechaPago, new DateOnly(2026, 10, 1))));
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GtDbContext>();
        var repo = new RepositorioAnalisisReportes(db, scope.ServiceProvider.GetRequiredService<CoordinadorFotoAnalisis>());
        var periodo = new PeriodoAnalisis(new(2026, 10, 1), new(2026, 10, 31));
        var contexto = ContextoAnalisis.DesdeInstante(new(2026, 11, 2, 12, 0, 0, TimeSpan.Zero), periodo, CriterioTemporalAnalisis.FechaPago);
        var filtro = new FiltroPagosAnalisis(periodo);
        var antes = await repo.PagosAsync(filtro, contexto);
        Assert.Equal(413m, antes.Resumen.ImportePagadoVigente);
        Assert.Equal(4, antes.Resumen.CantidadPagosVigentes);
        Assert.Equal(3, antes.Detalle.Items.Count(o => o.OppId == pago.OrdenId));
        Assert.Equal("2026-09", Assert.Single(antes.Detalle.Items, o => o.LiquidacionId == manual.Id).PeriodoLiquidado);
        using(var escritura = app.Services.CreateScope())
        {
            var pagos = escritura.ServiceProvider.GetRequiredService<IRepositorioPagos>();
            await pagos.AnularAsync(pago.OrdenId, new(Guid.NewGuid(), "Reversión posterior al rango", true), pago.UsuarioId, false, new(2026, 11, 1), new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));
            var nueva = pago.Generar with { ClaveOperacion = Guid.NewGuid(), Fecha = new(2026, 11, 1),
                Medios = [new ChequeEntrada(Guid.NewGuid(), 363m, ((ChequeEntrada)pago.Generar.Medios[0]).BancoId, Guid.NewGuid().ToString(), new(2026,11,1), new(2026,11,2), null)] };
            await pagos.GenerarAsync(nueva, pago.UsuarioId, false, new(2026,11,1), new(2026,11,1,13,0,0,DateTimeKind.Utc));
        }
        var despues = await repo.PagosAsync(filtro, contexto);
        Assert.Equal(50m, despues.Resumen.ImportePagadoVigente);
        Assert.Equal(363m, despues.Resumen.ImporteRevertidoHistorico);
        Assert.Equal(3, despues.Resumen.CantidadRevertidos);
        Assert.Equal(4, despues.Detalle.Total);
        Assert.All(despues.Detalle.Items.Where(o => o.OppId == pago.OrdenId), o =>
        {
            Assert.False(o.Vigente); Assert.NotNull(o.RevertidaEn); Assert.NotNull(o.RevertidaPor);
            Assert.Equal("opp", o.Origen); Assert.StartsWith("OPP-", o.OppNumero);
        });
        Assert.Equal("manual", Assert.Single((await repo.PagosAsync(filtro with { Vigencia = "vigentes" }, contexto)).Detalle.Items).Origen);
        Assert.Equal(3, (await repo.PagosAsync(filtro with { Vigencia = "revertidos" }, contexto)).Detalle.Total);
        Assert.Equal(50m, Assert.Single(despues.Series).ImportePagadoVigente);
    }
}
