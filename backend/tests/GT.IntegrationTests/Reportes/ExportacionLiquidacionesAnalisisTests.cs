using GT.Application.Pagos;
using GT.Application.Reportes.Analisis;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Liquidaciones;
using GT.IntegrationTests.Pagos;
using GT.IntegrationTests.Viajes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class ExportacionLiquidacionesAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task MesesCompletos_DiasExactos_ManualParcialYOppRevertidaSinDobleSuma()
    {
        var pago = await EscenarioAnulacion.CrearAsync(app, "fletero");
        var cliente = await app.CrearClienteAsync(); var transportista = await app.TransportistaExternoAsync();
        var viaje = await app.ViajeDeAsync(cliente.Id, transportista.Id, importe: 100m, fecha: new(2026, 9, 1));
        var manual = await app.CrearLiquidacionAsync(transportista.Id, [viaje], importePagado: 50m, mes: 9);
        await app.EnLaBaseAsync(db => db.OrdenesDePago.Where(o => o.LiquidacionId == manual.Id).ExecuteUpdateAsync(s => s.SetProperty(o => o.FechaPago, new DateOnly(2026, 10, 1))));
        using var scope = app.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IExportacionAnalisis>();
        var instante = new DateTimeOffset(2026, 11, 2, 12, 0, 0, TimeSpan.Zero);
        var periodo = new PeriodoAnalisis(new(2026, 9, 15), new(2026, 10, 10));
        var documento = await repo.ExportarLiquidacionesAsync(new(periodo, transportista.Id, Pagina: 2), instante, "gerencia");
        var fila = Assert.Single(documento.Filas);
        Assert.Equal("2026-09", fila[1].ValorTexto);
        Assert.Equal(100m, fila[4].ValorImporte); Assert.Equal(50m, fila[5].ValorImporte); Assert.Equal(50m, fila[6].ValorImporte);
        Assert.Contains("meses completos", documento.Encabezado.Filtros);
        var octubre = new PeriodoAnalisis(new(2026, 10, 1), new(2026, 10, 31));
        var antes = await repo.ExportarPagosAsync(new(octubre, Pagina: 2), instante, "gerencia");
        Assert.Equal(4, antes.Filas.Count);
        Assert.Equal(413m, antes.Totales.Single(t => t.Etiqueta == "Pagado vigente en el período").Importe);
        await scope.ServiceProvider.GetRequiredService<IRepositorioPagos>().AnularAsync(pago.OrdenId,
            new(Guid.NewGuid(), "Reversión posterior al período", true), pago.UsuarioId, false,
            new(2026, 11, 1), new(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));
        var despues = await repo.ExportarPagosAsync(new(octubre), instante, "gerencia");
        Assert.Equal(4, despues.Filas.Count);
        Assert.Equal(3, despues.Filas.Count(f => f[6].ValorTexto == "OPP" && f[8].ValorTexto == "Revertida"));
        Assert.Single(despues.Filas, f => f[6].ValorTexto == "Manual" && f[8].ValorTexto == "Vigente");
        Assert.Equal(50m, despues.Totales.Single(t => t.Etiqueta == "Pagado vigente en el período").Importe);
        Assert.Equal(363m, despues.Totales.Single(t => t.Etiqueta.StartsWith("Importe revertido")).Importe);
        var corto = await repo.ExportarPagosAsync(new(periodo), instante, "gerencia");
        Assert.Single(corto.Filas); // El pago OPP del18/10 queda fuera; la LQ09 documental sí estaba incluida.
    }
}
