using GT.Application.Flota;
using GT.Application.Reportes.Analisis;
using GT.Domain.Flota;
using GT.Domain.Viajes;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Flota;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Liquidaciones;
using GT.IntegrationTests.Viajes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class FlotaAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task AtributosActuales_DisponibilidadDominioEnLote_RankingEstableYSinVehiculoSeparado()
    {
        var dueno = await app.TransportistaExternoAsync();
        var nuevoDueno = await app.TransportistaExternoAsync();
        var tipo = await app.CrearTipoVehiculoAsync();
        var nuevoTipo = await app.CrearTipoVehiculoAsync();
        var a = await app.CrearVehiculoAsync(tipo.Id,dueno.Id,estadoOperativo:VehiculoEstado.Disponible);
        var b = await app.CrearVehiculoAsync(tipo.Id,dueno.Id,activo:false);
        var cero = await app.CrearVehiculoAsync(tipo.Id,dueno.Id);
        var docTipo = await app.CrearTipoDocumentacionDeVehiculoAsync();
        await app.CrearDocumentoVehiculoAsync(a.Id,docTipo.Id,-10);
        await app.CrearDocumentoVehiculoAsync(a.Id,docTipo.Id,50);
        var cliente = await app.CrearClienteAsync();
        await app.CrearViajeAsync(cliente.Id,new(2026,10,18),EstadoViaje.Rendido,vehiculoId:a.Id,transportistaId:dueno.Id);
        await app.CrearViajeAsync(cliente.Id,new(2026,10,18),EstadoViaje.Facturado,vehiculoId:b.Id,transportistaId:dueno.Id);
        await app.CrearViajeAsync(cliente.Id,new(2026,10,18),EstadoViaje.Rendido);
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GtDbContext>();
        var repo = new RepositorioAnalisisReportes(db,scope.ServiceProvider.GetRequiredService<CoordinadorFotoAnalisis>());
        var periodo = new PeriodoAnalisis(new(2026,10,1),new(2026,10,31));
        var contexto = ContextoAnalisis.Capturar(TimeProvider.System,periodo,CriterioTemporalAnalisis.FechaViaje);
        var datos = await repo.FlotaAsync(new(periodo,TransportistaId:dueno.Id),contexto);
        Assert.Equal(3,datos.Resumen.CantidadUnidades);
        Assert.Equal(2,datos.Resumen.TotalViajesConVehiculo);
        Assert.Equal(1,datos.Resumen.SinVehiculoEnPeriodo);
        Assert.Equal(2,Assert.Single(datos.Series).CantidadViajes);
        Assert.Equal(2,Assert.Single(datos.Distribuciones.Tipo.Top).Cantidad);
        Assert.Equal(new[]{a,b}.OrderBy(v=>v.Patente).Select(v=>v.Id),datos.Detalle.Items.Take(2).Select(v=>v.Id));
        var entidad = await db.Vehiculos.Include(v=>v.Documentacion).ThenInclude(d=>d.Tipo).SingleAsync(v=>v.Id==a.Id);
        var dominio = CalculadorEstadoOperativo.Derivar(entidad.EstadoOperativo,CalculadorEstadoVehiculo.Calcular(entidad.Documentacion,contexto.HoyArgentina));
        Assert.Equal(NombresDeEstadoFlota.DelVehiculo(dominio),Assert.Single(datos.Detalle.Items,v=>v.Id==a.Id).DisponibilidadActual);
        Assert.Equal("disponible",Assert.Single(datos.Detalle.Items,v=>v.Id==a.Id).DisponibilidadActual);
        Assert.Equal("fueraDeServicio",Assert.Single(datos.Detalle.Items,v=>v.Id==cero.Id).DisponibilidadActual);
        await app.EnLaBaseAsync(baseDatos=>baseDatos.Vehiculos.Where(v=>v.Id==a.Id).ExecuteUpdateAsync(s=>s.SetProperty(v=>v.TransportistaId,nuevoDueno.Id).SetProperty(v=>v.TipoVehiculoId,nuevoTipo.Id)));
        var actuales = await repo.FlotaAsync(new(periodo,TipoVehiculoId:nuevoTipo.Id,TransportistaId:nuevoDueno.Id),contexto);
        var fila = Assert.Single(actuales.Detalle.Items);
        Assert.Equal(a.Id,fila.Id); Assert.Equal(1,fila.CantidadViajes);
        Assert.Equal(nuevoDueno.Id,fila.Transportista.Id); Assert.Equal(nuevoTipo.Id,fila.Tipo.Id);
        Assert.Equal(1,actuales.Resumen.SinVehiculoEnPeriodo);
    }
}
