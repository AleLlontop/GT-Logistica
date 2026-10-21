using System.Globalization;
using System.Net;
using System.Text.Json;
using GT.Application.Reportes.Analisis;
using GT.Domain.Facturacion;
using GT.Domain.Liquidaciones;
using GT.Domain.Viajes;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Flota;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Liquidaciones;
using GT.IntegrationTests.Viajes;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class GruposAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task Top10YOtrosSql_ReconcilianTodasPerspectivas_NuloEsOtraIdentidad_YGruposCompletosPaginan()
    {
        var clientes = new List<int>();
        var fecha = new DateOnly(2026,10,18);
        for(var i=1;i<=21;i++)
        {
            var cliente = await app.CrearClienteAsync(razonSocial:"Categoría analítica"); clientes.Add(cliente.Id);
            var propietario = await app.TransportistaExternoAsync(nombre:"Propietario analítico");
            var tipo = await app.CrearTipoVehiculoAsync(nombre:"Tipo analítico");
            var vehiculo = await app.CrearVehiculoAsync(tipo.Id,propietario.Id);
            var viaje = await app.CrearViajeAsync(cliente.Id,fecha,EstadoViaje.Rendido,importe:i,transportistaId:propietario.Id,vehiculoId:vehiculo.Id);
            await app.CrearFacturaAsync(cliente.Id,fecha:fecha,estado:EstadoFactura.Pagada,fechaCobro:fecha,neto:i);
            await app.CrearLiquidacionAsync(propietario.Id,[viaje],estado:EstadoLiquidacion.Pagada,mes:10);
        }
        await app.CrearViajeAsync(clientes[0],fecha,EstadoViaje.Rendido,importe:1000m);
        using var scope = app.Services.CreateScope();
        var repo = new RepositorioAnalisisReportes(scope.ServiceProvider.GetRequiredService<GtDbContext>(),scope.ServiceProvider.GetRequiredService<CoordinadorFotoAnalisis>());
        var periodo = new PeriodoAnalisis(new(2026,10,1),new(2026,10,31));
        var contexto = ContextoAnalisis.Capturar(TimeProvider.System,periodo,CriterioTemporalAnalisis.FechaViaje);
        var viajes = await repo.ViajesAsync(new(periodo),contexto);
        static decimal Suma(DistribucionImporte d) => d.Top.Sum(g=>g.Importe)+(d.Otros?.Importe??0m);
        static void Recorte(DistribucionImporte d,decimal total)
        {
            Assert.Equal(10,d.Top.Count); Assert.NotNull(d.Otros);
            Assert.Equal("otros",d.Otros.Id); Assert.Equal(total,Suma(d));
        }
        Recorte(viajes.Distribuciones.Cliente,1231m);
        Recorte(viajes.Distribuciones.Transportista,1231m);
        Assert.Equal(22,viajes.Distribuciones.Transportista.TotalCategorias);
        Assert.Contains(viajes.Distribuciones.Transportista.Top,g=>g.Id is null && g.Nombre=="Sin asignar");
        var documentos = await repo.FacturacionAsync(new(periodo),contexto with {CriterioTemporal=CriterioTemporalAnalisis.FechaFactura});
        Recorte(documentos.Distribuciones.Cliente,279.51m);
        var cobros = await repo.CobrosAsync(new(periodo),contexto with {CriterioTemporal=CriterioTemporalAnalisis.FechaCobro});
        Recorte(cobros.Distribuciones.Cliente,279.51m);
        var liquidaciones = await repo.LiquidacionesAsync(new(periodo),contexto with {CriterioTemporal=CriterioTemporalAnalisis.PeriodoLiquidado});
        Recorte(liquidaciones.Distribuciones.Transportista,231m);
        var pagos = await repo.PagosAsync(new(periodo),contexto with {CriterioTemporal=CriterioTemporalAnalisis.FechaPago});
        Recorte(pagos.Distribuciones.Transportista,231m);
        var flota = await repo.FlotaAsync(new(periodo),contexto);
        Assert.Equal(10,flota.Distribuciones.Tipo.Top.Count);
        Assert.Equal(11,flota.Distribuciones.Tipo.Otros!.Cantidad);
        Assert.Equal(21,flota.Distribuciones.Tipo.Top.Sum(g=>g.Cantidad)+flota.Distribuciones.Tipo.Otros.Cantidad);
        var filtro = new FiltroGrupoAnalisis(new FiltroViajesAnalisis(periodo),"cliente");
        var primera = await repo.ImportesViajesAsync(filtro,contexto,CancellationToken.None);
        var segunda = await repo.ImportesViajesAsync(filtro with {Pagina=2},contexto,CancellationToken.None);
        Assert.Equal(21,primera.Detalle.Total); Assert.Single(segunda.Detalle.Items);
        var todos = primera.Detalle.Items.Concat(segunda.Detalle.Items).ToArray();
        Assert.Equal(21,todos.Select(g=>g.Id).Distinct().Count());
        Assert.Equal(1231m,todos.Sum(g=>g.Importe));
        Assert.Equal(clientes.Order(),todos.Select(g=>int.Parse(g.Id!,CultureInfo.InvariantCulture)));
        using var http = await app.CrearClienteAutenticadoAsync();
        using var respuesta = await http.GetAsync("/api/reportes/viajes/grupos?desde=2026-10-01&hasta=2026-10-31&dimension=cliente&pagina=2");
        Assert.Equal(HttpStatusCode.OK,respuesta.StatusCode);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(21,json.RootElement.GetProperty("detalle").GetProperty("total").GetInt32());
        var referencias = await repo.ReferenciasViajesAsync(false,"Categoría analítica",new(),CancellationToken.None);
        Assert.Equal(21,referencias.Total); Assert.Equal(20,referencias.Items.Count);
    }
}
