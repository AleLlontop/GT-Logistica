using GT.Application.Reportes.Analisis;
using GT.Domain.Facturacion;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class FacturacionAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task Documentos_FiltraEstadosExclusivos_RespetaSnapshotYNoDuplicaRefacturada()
    {
        var cliente = await app.CrearClienteAsync(razonSocial: "Nombre actual", activo: false);
        var fecha = new DateOnly(2026, 10, 10);
        var pendiente = await app.CrearFacturaAsync(cliente.Id, fecha: fecha, neto: 100m, vencimientoPago: new(2026, 10, 19));
        var vencida = await app.CrearFacturaAsync(cliente.Id, fecha: fecha, neto: 200m, vencimientoPago: new(2026, 10, 18));
        var pagada = await app.CrearFacturaAsync(cliente.Id, fecha: fecha, neto: 300m, estado: EstadoFactura.Pagada, fechaCobro: new(2026, 10, 18));
        var anulada = await app.CrearFacturaAsync(cliente.Id, fecha: fecha, neto: 400m, estado: EstadoFactura.Anulada, motivoAnulacion: "Refacturada");
        var reemplazo = await app.CrearFacturaAsync(cliente.Id, fecha: fecha, neto: 400m, tipoFacturacion: TipoFacturacion.Refacturacion, facturaReemplazadaId: anulada.Id);
        await app.CrearFacturaAsync(cliente.Id, fecha: new(2026, 9, 30), neto: 999m);
        using var scope = app.Services.CreateScope();
        var repo = new RepositorioAnalisisReportes(scope.ServiceProvider.GetRequiredService<GtDbContext>(), scope.ServiceProvider.GetRequiredService<CoordinadorFotoAnalisis>());
        var periodo = new PeriodoAnalisis(new(2026, 10, 1), new(2026, 10, 31));
        var contexto = ContextoAnalisis.DesdeInstante(new(2026, 10, 19, 12, 0, 0, TimeSpan.Zero), periodo, CriterioTemporalAnalisis.FechaFactura);
        var filtro = new FiltroFacturacionAnalisis(periodo, ClienteId: cliente.Id);
        var datos = await repo.FacturacionAsync(filtro, contexto);
        Assert.Equal(4, datos.Resumen.Cantidad);
        Assert.Equal(1210m, datos.Resumen.Facturado);
        Assert.Equal(363m, datos.Resumen.CobradoVigenteDocumental);
        Assert.Equal(847m, datos.Resumen.PendienteVigenteDocumental);
        Assert.All(datos.Detalle.Items, f => Assert.Equal("Distribuidora del Litoral", f.ClienteRazonSocial));
        Assert.Equal(cliente.RazonSocial, Assert.Single(datos.Distribuciones.Cliente.Top).Nombre);
        foreach(var estado in new[]{"pendiente", "vencida", "pagada", "anulada"})
        {
            var filtrado = await repo.FacturacionAsync(filtro with { Estado = estado }, contexto);
            Assert.All(filtrado.Detalle.Items, f => Assert.Equal(estado, f.Estado));
            Assert.NotEmpty(filtrado.Detalle.Items);
            if (estado == "anulada")
            {
                Assert.Equal(484m, Assert.Single(filtrado.Detalle.Items).Total);
                Assert.Equal(0m, filtrado.Resumen.Facturado);
                Assert.Equal(0m, filtrado.Resumen.PendienteVigenteDocumental);
            }
        }
        Assert.Equal(2, (await repo.FacturacionAsync(filtro with { Estado = "pendiente" }, contexto)).Detalle.Total);
        Assert.Equal(1, (await repo.FacturacionAsync(filtro with { Estado = "vencida" }, contexto)).Detalle.Total);
        Assert.Equal(1210m, datos.Series.Sum(m => m.Facturado));
        Assert.Equal(4, datos.Distribuciones.Estado.Top.Sum(g => g.Cantidad));
    }
}
