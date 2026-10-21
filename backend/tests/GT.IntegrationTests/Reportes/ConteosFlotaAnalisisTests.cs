using GT.Application.Reportes.Analisis;
using GT.Domain.Viajes;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Flota;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Liquidaciones;
using GT.IntegrationTests.Viajes;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class ConteosFlotaAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task LeftJoinIncluyeCerosYBajas_CuentaSoloRealizados_OrdenTotal()
    {
        var propietario = await app.TransportistaExternoAsync();
        var tipo = await app.CrearTipoVehiculoAsync();
        var a = await app.CrearVehiculoAsync(tipo.Id, propietario.Id);
        var b = await app.CrearVehiculoAsync(tipo.Id, propietario.Id, activo: false);
        var cero = await app.CrearVehiculoAsync(tipo.Id, propietario.Id);
        var cliente = await app.CrearClienteAsync();
        var fecha = new DateOnly(2026,10,18);
        await app.CrearViajeAsync(cliente.Id,fecha,EstadoViaje.Rendido,vehiculoId:a.Id);
        await app.CrearViajeAsync(cliente.Id,fecha,EstadoViaje.Facturado,vehiculoId:a.Id);
        await app.CrearViajeAsync(cliente.Id,fecha,EstadoViaje.Rendido,vehiculoId:b.Id);
        await app.CrearViajeAsync(cliente.Id,fecha,EstadoViaje.Pendiente,vehiculoId:cero.Id);
        await app.CrearViajeAsync(cliente.Id,fecha,EstadoViaje.EnCurso,vehiculoId:cero.Id);
        await app.CrearViajeAsync(cliente.Id,fecha,EstadoViaje.Anulado,vehiculoId:cero.Id,motivoAnulacion:"Prueba");
        await app.CrearViajeAsync(cliente.Id,new(2026,9,30),EstadoViaje.Rendido,vehiculoId:cero.Id);
        await app.CrearViajeAsync(cliente.Id,fecha,EstadoViaje.Rendido);
        using var scope = app.Services.CreateScope();
        var repo = new RepositorioAnalisisReportes(scope.ServiceProvider.GetRequiredService<GtDbContext>(),scope.ServiceProvider.GetRequiredService<CoordinadorFotoAnalisis>());
        var filtro = new FiltroFlotaAnalisis(new(new(2026,10,1),new(2026,10,31)),TransportistaId:propietario.Id,TamanioPagina:1);
        var primera = await repo.ConteosFlotaAsync(filtro);
        Assert.Equal(3,primera.Total);
        Assert.Equal(a.Id.ToString(),Assert.Single(primera.Items).Id);
        Assert.Equal(2,primera.Items[0].Cantidad);
        Assert.Equal(b.Id.ToString(),Assert.Single((await repo.ConteosFlotaAsync(filtro with {Pagina=2})).Items).Id);
        Assert.Equal(cero.Id.ToString(),Assert.Single((await repo.ConteosFlotaAsync(filtro with {Orden="cantidadAsc"})).Items).Id);
        Assert.Equal(1,(await repo.ConteosFlotaAsync(filtro with {Activo=false})).Total);
        Assert.Equal(2,(await repo.ConteosFlotaAsync(filtro with {Activo=true})).Total);
        Assert.Equal(1,Assert.Single((await repo.ConteosFlotaAsync(filtro with {VehiculoId=b.Id})).Items).Cantidad);
    }
}
