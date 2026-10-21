using GT.Application.Reportes.Analisis;
using GT.Domain.Facturacion;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class ExportacionFacturacionAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task DocumentoYFlujoExportanUniversosSeparados_SnapshotYRefacturacionSinDobleVenta()
    {
        var cliente = await app.CrearClienteAsync(razonSocial: "Cliente actual", activo: false);
        var anterior = await app.CrearFacturaAsync(cliente.Id, fecha: new(2026, 9, 10), estado: EstadoFactura.Pagada, fechaCobro: new(2026, 10, 5), neto: 100.01m);
        var pendiente = await app.CrearFacturaAsync(cliente.Id, fecha: new(2026, 10, 10), neto: 200.01m);
        var anulada = await app.CrearFacturaAsync(cliente.Id, fecha: new(2026, 10, 10), neto: 300.01m, estado: EstadoFactura.Anulada, motivoAnulacion: "Refacturación");
        var reemplazo = await app.CrearFacturaAsync(cliente.Id, fecha: new(2026, 10, 10), neto: 300.01m, tipoFacturacion: TipoFacturacion.Refacturacion, facturaReemplazadaId: anulada.Id);
        using var scope = app.Services.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IExportacionAnalisis>();
        var periodo = new PeriodoAnalisis(new(2026, 10, 1), new(2026, 10, 31));
        var instante = new DateTimeOffset(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);
        var documentos = await repo.ExportarFacturacionAsync(new(periodo, cliente.Id, Pagina: 2), instante, "gerencia");
        Assert.Equal(2, documentos.Filas.Count);
        Assert.DoesNotContain(documentos.Filas, f => f[0].ValorTexto == anterior.NumeroComprobante || f[0].ValorTexto == anulada.NumeroComprobante);
        Assert.All(documentos.Filas, f => Assert.Equal("Distribuidora del Litoral", f[2].ValorTexto));
        Assert.Equal(pendiente.Total + reemplazo.Total, documentos.Totales.Single(t => t.Etiqueta == "Facturado vigente").Importe);
        Assert.Equal(pendiente.Neto + reemplazo.Neto, documentos.Totales.Single(t => t.Etiqueta == "Neto vigente").Importe);
        Assert.Equal(pendiente.Iva + reemplazo.Iva, documentos.Totales.Single(t => t.Etiqueta == "IVA vigente").Importe);
        Assert.Contains(cliente.RazonSocial, documentos.Encabezado.Filtros);
        var cobros = await repo.ExportarCobrosAsync(new(periodo, cliente.Id, Pagina: 2), instante, "gerencia");
        var cobro = Assert.Single(cobros.Filas);
        Assert.Equal(anterior.NumeroComprobante, cobro[0].ValorTexto);
        Assert.Equal(new DateOnly(2026, 9, 10), cobro[2].ValorFecha);
        Assert.Equal(new DateOnly(2026, 10, 5), cobro[3].ValorFecha);
        Assert.Equal(anterior.Total, cobros.Totales.Single(t => !t.EsCantidad).Importe);
        var historia = await repo.ExportarFacturacionAsync(new(periodo, cliente.Id, Estado: "anulada"), instante, "gerencia");
        Assert.Equal(anulada.Total, Assert.Single(historia.Filas)[6].ValorImporte);
        Assert.All(historia.Totales.Where(t => !t.EsCantidad), t => Assert.Equal(0m, t.Importe));
    }
}
