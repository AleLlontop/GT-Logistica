using GT.Application.Reportes.Analisis;
using GT.Domain.Viajes;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class TablaViajesAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task FiltraAntesDePaginar_OrdenTotal_EstadosYBusquedaComoListado005()
    {
        var cliente=await app.CrearClienteAsync(activo:false);
        var ajeno=await app.CrearClienteAsync();
        for(var i=0;i<21;i++)await app.CrearViajeAsync(cliente.Id,new(2026,10,18),EstadoViaje.Rendido,destino:"Córdoba",importe:121m);
        await app.CrearViajeAsync(cliente.Id,new(2026,10,18),EstadoViaje.Facturado,importe:242m);
        await app.CrearViajeAsync(cliente.Id,new(2026,10,18),EstadoViaje.EnCurso);
        await app.CrearViajeAsync(cliente.Id,new(2026,10,18),EstadoViaje.Pendiente);
        await app.CrearViajeAsync(cliente.Id,new(2026,10,18),EstadoViaje.Anulado,motivoAnulacion:"Anulado de prueba");
        await app.CrearViajeAsync(cliente.Id,new(2026,9,18),EstadoViaje.Rendido);
        await app.CrearViajeAsync(ajeno.Id,new(2026,10,18),EstadoViaje.Rendido);
        using var scope=app.Services.CreateScope();
        var repo=new RepositorioAnalisisReportes(scope.ServiceProvider.GetRequiredService<GtDbContext>(),scope.ServiceProvider.GetRequiredService<CoordinadorFotoAnalisis>());
        var filtro=new FiltroViajesAnalisis(new(new(2026,10,1),new(2026,10,31)),ClienteId:cliente.Id,Estado:"rendido",Busqueda:"cordoba");
        var primera=await repo.TablaViajesAsync(filtro);
        var segunda=await repo.TablaViajesAsync(filtro with {Pagina=2});
        Assert.Equal(21,primera.Total);
        Assert.Equal(20,primera.Items.Count);
        Assert.Single(segunda.Items);
        Assert.Empty(primera.Items.Select(v=>v.Id).Intersect(segunda.Items.Select(v=>v.Id)));
        Assert.All(primera.Items,v=>Assert.False(v.Cliente.Activo));
        Assert.True(primera.Items[0].Numero>segunda.Items[0].Numero);
        Assert.Equal(24,(await repo.TablaViajesAsync(filtro with {Estado=null,Busqueda=null})).Total);
        foreach(var estado in new[]{"pendiente","enCurso","facturado","anulado"})
            Assert.Single((await repo.TablaViajesAsync(filtro with {Estado=estado,Busqueda=null})).Items);
    }
    [Fact]
    public async Task AgregadosCompletos_NoDependenDePagina_YMesVacioEsCero()
    {
        var cliente = await app.CrearClienteAsync(activo: false);
        for (var i = 0; i < 200; i++)
            await app.CrearViajeAsync(cliente.Id, new(2026, 10, 18), i % 2 == 0 ? EstadoViaje.Rendido : EstadoViaje.Facturado, importe: 0.01m);
        await app.CrearViajeAsync(cliente.Id, new(2026, 10, 18), EstadoViaje.EnCurso, importe: 3m);
        await app.CrearViajeAsync(cliente.Id, new(2026, 10, 18), EstadoViaje.Pendiente, importe: 4m);
        await app.CrearViajeAsync(cliente.Id, new(2026, 10, 18), EstadoViaje.Anulado, importe: 999m, motivoAnulacion: "Prueba");
        using var scope = app.Services.CreateScope();
        var repo = new RepositorioAnalisisReportes(scope.ServiceProvider.GetRequiredService<GtDbContext>(), scope.ServiceProvider.GetRequiredService<CoordinadorFotoAnalisis>());
        var periodo = new PeriodoAnalisis(new(2026, 9, 1), new(2026, 10, 31));
        var filtro = new FiltroViajesAnalisis(periodo, ClienteId: cliente.Id);
        var contexto = ContextoAnalisis.DesdeInstante(new DateTimeOffset(2026, 10, 19, 0, 0, 0, TimeSpan.Zero), periodo, CriterioTemporalAnalisis.FechaViaje);
        var primera = await repo.ViajesAsync(filtro, contexto);
        var ultima = await repo.ViajesAsync(filtro with { Pagina = 11 }, contexto);
        Assert.Equal(primera.Resumen, ultima.Resumen);
        Assert.Equal(202, primera.Resumen.Cantidad);
        Assert.Equal(200, primera.Resumen.Realizados);
        Assert.Equal(1, primera.Resumen.PendientesRendicion);
        Assert.Equal(1, primera.Resumen.Pendientes);
        Assert.Equal(9m, primera.Resumen.ImporteTotal);
        Assert.Equal(0, primera.Series[0].Cantidad);
        Assert.Equal(202, primera.Series[1].Cantidad);
        Assert.Equal(202, primera.Distribuciones.Estado.Top.Sum(g => g.Cantidad));
        Assert.Equal(9m, primera.Distribuciones.Cliente.Top.Sum(g => g.Importe));
        Assert.Null(Assert.Single(primera.Distribuciones.Transportista.Top).Id);
        var anulados = await repo.ViajesAsync(filtro with { Estado = "anulado" }, contexto);
        Assert.Equal(1, anulados.Resumen.Anulados);
        Assert.Equal(0, anulados.Resumen.Realizados);
        Assert.Equal(0m, anulados.Resumen.ImporteTotal);
    }
}
