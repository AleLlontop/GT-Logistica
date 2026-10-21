using GT.Application.Reportes.Analisis;
using GT.Domain.Facturacion;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class CobrosAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task FechaCobroIncluyeDocumentoAnterior_SinRestarloDeDeudaDocumental()
    {
        var cliente = await app.CrearClienteAsync(activo: false);
        var anterior = await app.CrearFacturaAsync(cliente.Id, fecha: new(2026, 9, 10), estado: EstadoFactura.Pagada, fechaCobro: new(2026, 10, 1), neto: 100m);
        var ultimo = await app.CrearFacturaAsync(cliente.Id, fecha: new(2026, 9, 10), estado: EstadoFactura.Pagada, fechaCobro: new(2026, 10, 31), neto: 200m);
        var pendiente = await app.CrearFacturaAsync(cliente.Id, fecha: new(2026, 10, 10), neto: 300m);
        await app.CrearFacturaAsync(cliente.Id, fecha: new(2026, 9, 10), estado: EstadoFactura.Pagada, fechaCobro: new(2026, 9, 30));
        await app.CrearFacturaAsync(cliente.Id, fecha: new(2026, 10, 10), estado: EstadoFactura.Pagada, fechaCobro: new(2026, 11, 1), neto: 400m);
        await app.CrearFacturaAsync(cliente.Id, fecha: new(2026, 10, 10), estado: EstadoFactura.Anulada, motivoAnulacion: "Prueba");
        var ajeno = await app.CrearClienteAsync();
        await app.CrearFacturaAsync(ajeno.Id, fecha: new(2026, 10, 10), estado: EstadoFactura.Pagada, fechaCobro: new(2026, 10, 10));
        using var scope = app.Services.CreateScope();
        var repo = new RepositorioAnalisisReportes(scope.ServiceProvider.GetRequiredService<GtDbContext>(), scope.ServiceProvider.GetRequiredService<CoordinadorFotoAnalisis>());
        var periodo = new PeriodoAnalisis(new(2026, 10, 1), new(2026, 10, 31));
        var contexto = ContextoAnalisis.DesdeInstante(new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero), periodo, CriterioTemporalAnalisis.FechaCobro);
        var filtro = new FiltroCobrosAnalisis(periodo, ClienteId: cliente.Id, TamanioPagina: 1);
        var cobros = await repo.CobrosAsync(filtro, contexto);
        Assert.Equal(2, cobros.Resumen.CantidadCobros);
        Assert.Equal(anterior.Total + ultimo.Total, cobros.Resumen.ImporteCobrado);
        Assert.Equal(ultimo.Id, Assert.Single(cobros.Detalle.Items).FacturaId);
        Assert.Equal(9, cobros.Detalle.Items[0].FechaFactura.Month);
        Assert.Equal(10, cobros.Detalle.Items[0].FechaCobro.Month);
        Assert.Equal(cobros.Resumen.ImporteCobrado, Assert.Single(cobros.Series).ImporteCobrado);
        var segunda = await repo.CobrosAsync(filtro with { Pagina = 2 }, contexto);
        Assert.Equal(anterior.Id, Assert.Single(segunda.Detalle.Items).FacturaId);
        Assert.Equal(cobros.Resumen, segunda.Resumen);
        var documentos = await repo.FacturacionAsync(new(periodo, ClienteId: cliente.Id), contexto with { CriterioTemporal = CriterioTemporalAnalisis.FechaFactura });
        Assert.Equal(pendiente.Total, documentos.Resumen.PendienteVigenteDocumental);
        Assert.Single((await repo.CobrosAsync(filtro with { Numero = anterior.NumeroComprobante }, contexto)).Detalle.Items);
        var vacio = await repo.CobrosAsync(filtro with { Numero = "inexistente" }, contexto);
        Assert.Equal(0m, vacio.Resumen.ImporteCobrado);
        Assert.Equal(0m, Assert.Single(vacio.Series).ImporteCobrado);
        Assert.Empty(vacio.Detalle.Items);
    }
}
