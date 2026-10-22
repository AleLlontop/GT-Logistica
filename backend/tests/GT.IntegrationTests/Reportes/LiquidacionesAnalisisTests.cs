using GT.Application.Reportes.Analisis;
using GT.Domain.Liquidaciones;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Liquidaciones;
using GT.IntegrationTests.Viajes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class LiquidacionesAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task MesesCompletos_ImportesVigentes_SinMultiplicarPorViajesYAnuladaSinDeuda()
    {
        var cliente = await app.CrearClienteAsync();
        var fletero = await app.TransportistaExternoAsync(activo: false);
        var viaje1 = await app.ViajeDeAsync(cliente.Id, fletero.Id, importe: 100m, fecha: new(2026, 9, 1));
        var viaje2 = await app.ViajeDeAsync(cliente.Id, fletero.Id, importe: 200m, fecha: new(2026, 9, 30));
        var septiembre = await app.CrearLiquidacionAsync(fletero.Id, [viaje1, viaje2], importePagado: 50m, mes: 9, generadaEn: new(2026, 11, 1));
        var viaje3 = await app.ViajeDeAsync(cliente.Id, fletero.Id, importe: 400m, fecha: new(2026, 10, 31));
        await app.CrearLiquidacionAsync(fletero.Id, [viaje3], estado: EstadoLiquidacion.Pagada, mes: 10);
        var viaje4 = await app.ViajeDeAsync(cliente.Id, fletero.Id, importe: 500m, fecha: new(2026, 10, 1));
        await app.CrearLiquidacionAsync(fletero.Id, [viaje4], estado: EstadoLiquidacion.Anulada, mes: 10);
        var viaje5 = await app.ViajeDeAsync(cliente.Id, fletero.Id, importe: 999m, fecha: new(2026, 8, 1));
        await app.CrearLiquidacionAsync(fletero.Id, [viaje5], mes: 8);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GtDbContext>();
        var repo = new RepositorioAnalisisReportes(db, scope.ServiceProvider.GetRequiredService<CoordinadorFotoAnalisis>());
        var periodo = new PeriodoAnalisis(new(2026, 9, 15), new(2026, 10, 10));
        var contexto = ContextoAnalisis.DesdeInstante(new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero), periodo, CriterioTemporalAnalisis.PeriodoLiquidado);
        var filtro = new FiltroLiquidacionesAnalisis(periodo, fletero.Id, TamanioPagina: 1);
        var datos = await repo.LiquidacionesAsync(filtro, contexto);
        Assert.Equal(2, datos.Resumen.Cantidad);
        Assert.Equal(700m, datos.Resumen.ImporteLiquidado);
        Assert.Equal(450m, datos.Resumen.PagadoVigenteDocumental);
        Assert.Equal(250m, datos.Resumen.PendienteVigenteDocumental);
        Assert.Equal(450m, await db.OrdenesDePago.Where(o => o.Liquidacion!.TransportistaId == fletero.Id && o.Vigente).SumAsync(o => o.Importe));
        Assert.Equal("2026-10", Assert.Single(datos.Detalle.Items).Periodo);
        Assert.False(datos.Detalle.Items[0].Transportista.Activo);
        var segunda = await repo.LiquidacionesAsync(filtro with { Pagina = 2 }, contexto);
        Assert.Equal(datos.Resumen, segunda.Resumen);
        Assert.Equal(septiembre.Id, Assert.Single(segunda.Detalle.Items).Id);
        Assert.Equal(new[]{"2026-09", "2026-10"}, datos.Series.Select(m => m.Periodo));
        Assert.Equal(700m, datos.Series.Sum(m => m.ImporteLiquidado));
        var anuladas = await repo.LiquidacionesAsync(filtro with { Estado = "anulada" }, contexto);
        Assert.Null(Assert.Single(anuladas.Detalle.Items).PendienteVigente);
        Assert.Equal(500m, anuladas.Detalle.Items[0].ImporteTotal);
        Assert.Equal(0m, anuladas.Resumen.ImporteLiquidado);
        Assert.Equal(0m, anuladas.Resumen.PendienteVigenteDocumental);
    }
}
