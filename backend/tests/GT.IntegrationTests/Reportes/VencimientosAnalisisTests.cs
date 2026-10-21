using System.Text.Json;
using GT.Application.Facturacion;
using GT.Application.Reportes.Analisis;
using GT.Domain.Facturacion;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Flota;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Liquidaciones;
using GT.IntegrationTests.Viajes;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class VencimientosAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task FachadaConservaUniversoUrgenciaYPaginaDeTresPaneles_UsaHoyCapturado()
    {
        var docChofer = await app.CrearTipoDocumentacionAsync();
        for(var i=0;i<21;i++)
        {
            var chofer = await app.CrearChoferCompletoAsync(90150000+i);
            await app.CrearDocumentoAsync(chofer.Id,docChofer.Id,-i-1);
        }
        var inactivo = await app.CrearChoferCompletoAsync(90150100,activo:false);
        await app.CrearDocumentoAsync(inactivo.Id,docChofer.Id,-100);
        var propietario = await app.TransportistaExternoAsync();
        var tipo = await app.CrearTipoVehiculoAsync();
        var vehiculo = await app.CrearVehiculoAsync(tipo.Id,propietario.Id);
        var docVehiculo = await app.CrearTipoDocumentacionDeVehiculoAsync();
        await app.CrearDocumentoVehiculoAsync(vehiculo.Id,docVehiculo.Id,-5);
        var cliente = await app.CrearClienteAsync();
        var contexto = ContextoAnalisis.Capturar(TimeProvider.System,null,CriterioTemporalAnalisis.SituacionActual);
        await app.CrearFacturaAsync(cliente.Id,fecha:contexto.HoyArgentina.AddDays(-10),vencimientoPago:contexto.HoyArgentina.AddDays(-1),neto:100m);
        await app.CrearFacturaAsync(cliente.Id,fecha:contexto.HoyArgentina,vencimientoPago:contexto.HoyArgentina.AddDays(7),neto:200m);
        await app.CrearFacturaAsync(cliente.Id,fecha:contexto.HoyArgentina,vencimientoPago:contexto.HoyArgentina.AddDays(8));
        await app.CrearFacturaAsync(cliente.Id,fecha:contexto.HoyArgentina,estado:EstadoFactura.Pagada,fechaCobro:contexto.HoyArgentina);
        using var scope = app.Services.CreateScope();
        var repo = new RepositorioAnalisisReportes(scope.ServiceProvider.GetRequiredService<GtDbContext>(),scope.ServiceProvider.GetRequiredService<CoordinadorFotoAnalisis>());
        var choferes = await repo.VencimientosChoferesAsync(new(),contexto);
        var pagina2 = await repo.VencimientosChoferesAsync(new(Pagina:2),contexto);
        var originales = await scope.ServiceProvider.GetRequiredService<GT.Application.Choferes.Documentacion.ConsultarVencimientos>().EjecutarAsync(fecha:contexto.HoyArgentina);
        Assert.Equal(21,choferes.Detalle.Total);
        Assert.Equal(20,choferes.Detalle.Items.Count);
        Assert.Single(pagina2.Detalle.Items);
        Assert.Equal(JsonSerializer.Serialize(originales),JsonSerializer.Serialize(choferes.Detalle.Items.Concat(pagina2.Detalle.Items).ToArray()));
        var flota = await repo.VencimientosFlotaAsync(new(),contexto);
        var originalFlota = await scope.ServiceProvider.GetRequiredService<GT.Application.Flota.Documentacion.ConsultarVencimientosFlota>().EjecutarAsync(fecha:contexto.HoyArgentina);
        Assert.Equal(JsonSerializer.Serialize(originalFlota),JsonSerializer.Serialize(flota.Detalle.Items));
        var facturas = await repo.VencimientosFacturasAsync(new(TamanioPagina:1),contexto);
        var originalesFacturas = await scope.ServiceProvider.GetRequiredService<IRepositorioFacturas>().ConsultarVencimientosAsync(contexto.HoyArgentina);
        Assert.Equal(2,facturas.Detalle.Total);
        Assert.Equal(363m,facturas.ImporteTotal);
        Assert.Equal(originalesFacturas[0],Assert.Single(facturas.Detalle.Items));
        Assert.Null(facturas.Contexto.Desde);
        Assert.Equal(CriterioTemporalAnalisis.SituacionActual,facturas.Contexto.CriterioTemporal);
    }
}
