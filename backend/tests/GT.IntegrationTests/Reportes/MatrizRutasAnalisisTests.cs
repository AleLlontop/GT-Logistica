using System.Net;
using System.Text.Json;
using GT.Api.Autorizacion;
using GT.Domain.Usuarios;
using GT.Infrastructure.Persistencia;
using GT.IntegrationTests.Facturacion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class MatrizRutasAnalisisTests(AplicacionObservadaAnalisis app) : IClassFixture<AplicacionObservadaAnalisis>
{
    private sealed record Ruta(string Url,string? Fuente);
    private static readonly Ruta[] Rutas =
    [
        new("dashboard",null),
        new("viajes",CodigosPermiso.ViajesConsultar),new("viajes/grupos",CodigosPermiso.ViajesConsultar),
        new("viajes/referencias/clientes",CodigosPermiso.ViajesConsultar),new("viajes/referencias/transportistas",CodigosPermiso.ViajesConsultar),
        new("facturacion",CodigosPermiso.FacturacionConsultar),new("facturacion/cobros",CodigosPermiso.FacturacionConsultar),
        new("facturacion/grupos",CodigosPermiso.FacturacionConsultar),new("facturacion/cobros/grupos",CodigosPermiso.FacturacionConsultar),new("facturacion/referencias/clientes",CodigosPermiso.FacturacionConsultar),
        new("liquidaciones",CodigosPermiso.LiquidacionesConsultar),new("liquidaciones/pagos",CodigosPermiso.LiquidacionesConsultar),
        new("liquidaciones/grupos",CodigosPermiso.LiquidacionesConsultar),new("liquidaciones/pagos/grupos",CodigosPermiso.LiquidacionesConsultar),new("liquidaciones/referencias/transportistas",CodigosPermiso.LiquidacionesConsultar),
        new("caja",CodigosPermiso.CajaConsultar),new("caja/grupos",CodigosPermiso.CajaConsultar),new("caja/referencias/cajas",CodigosPermiso.CajaConsultar),
        new("flota",CodigosPermiso.ReportesFlotaConsultar),new("flota/grupos",CodigosPermiso.ReportesFlotaConsultar),
        new("flota/referencias/vehiculos",CodigosPermiso.ReportesFlotaConsultar),new("flota/referencias/tipos",CodigosPermiso.ReportesFlotaConsultar),new("flota/referencias/transportistas",CodigosPermiso.ReportesFlotaConsultar),
        new("vencimientos/choferes",CodigosPermiso.ChoferesVencimientosConsultar),new("vencimientos/flota",CodigosPermiso.FlotaVencimientosConsultar),new("vencimientos/facturas",CodigosPermiso.FacturacionConsultar),
    ];
    private static string Url(Ruta ruta)
    {
        var url="/api/reportes/"+ruta.Url;
        if(ruta.Url.Contains("referencias") || ruta.Url.StartsWith("vencimientos"))return url;
        var dimension=ruta.Url.StartsWith("viajes") || ruta.Url.StartsWith("facturacion")?"cliente":ruta.Url.StartsWith("liquidaciones")?"transportista":"tipo";
        return url+"?desde=2026-10-01&hasta=2026-10-31&dimension="+dimension;
    }

    [Fact]
    public async Task InventarioCompleto_MatrizPermisos401403Uniformes_RevocacionYSinAccesoLateral()
    {
        var endpoints=app.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Select(e=>e.RoutePattern.RawText?.TrimEnd('/')).Where(p=>p?.StartsWith("/api/reportes/",StringComparison.Ordinal)==true && !p.EndsWith("/exportacion",StringComparison.Ordinal)).Order().ToArray();
        Assert.Equal(Rutas.Select(r=>"/api/reportes/"+r.Url).Order(),endpoints);
        using(var anonimo=app.CrearCliente())
        foreach(var ruta in Rutas)
        {
            using var respuesta=await anonimo.GetAsync(Url(ruta));
            Assert.Equal(HttpStatusCode.Unauthorized,respuesta.StatusCode);
        }
        var fuentes=Rutas.Select(r=>r.Fuente).OfType<string>().Distinct().ToArray();
        List<string[]> casos = [[], [CodigosPermiso.ReportesConsultar], [CodigosPermiso.ReportesEmitir]];
        casos.AddRange(fuentes.Select(f=>new[]{f}));
        casos.AddRange(fuentes.Select(f=>new[]{CodigosPermiso.ReportesConsultar,f}));
        casos.Add([CodigosPermiso.ReportesConsultar,..fuentes]);
        foreach(var permisos in casos)
        {
            var nombre="mat_"+Guid.NewGuid().ToString("N")[..10];
            var usuario=await app.CrearUsuarioConPermisosAsync(nombre,"Reportes.12345678",permisos);
            using var http=await app.CrearClienteAutenticadoAsync(nombre,"Reportes.12345678");
            foreach(var ruta in Rutas)
            {
                var autorizado=permisos.Contains(CodigosPermiso.ReportesConsultar) && (ruta.Fuente is null || permisos.Contains(ruta.Fuente));
                app.Sonda.Iniciar();
                using var respuesta=await http.GetAsync(Url(ruta));
                app.Sonda.Activa=false;
                Assert.Equal(autorizado?HttpStatusCode.OK:HttpStatusCode.Forbidden,respuesta.StatusCode);
                if(!autorizado)
                {
                    using var json=JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
                    Assert.Equal("sin_permiso",json.RootElement.GetProperty("codigo").GetString());
                    Assert.Equal("No tenés permiso para consultar o emitir este reporte. Pedíselo a quien administra el sistema.",json.RootElement.GetProperty("mensaje").GetString());
                    Assert.DoesNotContain(app.Sonda.Comandos,c=>new[]{"[Viajes]","[Clientes]","[Transportistas]","[Vehiculos]","[TiposVehiculo]","[Facturas]","[Liquidaciones]","[OrdenesDePago]","[Cajas]","[MovimientosDeCaja]","[Documentaciones]","[DocumentacionesVehiculo]"}.Any(c.Sql.Contains));
                }
            }
            if(permisos.Length==fuentes.Length+1)
            {
                foreach(var lateral in new[]{"/api/flota/vehiculos","/api/choferes","/api/pagos/bancos"})
                {
                    using var respuesta=await http.GetAsync(lateral);
                    Assert.Equal(HttpStatusCode.Forbidden,respuesta.StatusCode);
                }
                await app.EnLaBaseAsync(async db=>
                {
                    var rol=await db.Roles.Include(r=>r.Permisos).SingleAsync(r=>r.Id==usuario.Roles.Single().Id);
                    rol.Permisos.Clear();await db.SaveChangesAsync();
                });
                foreach(var ruta in Rutas)
                {
                    using var respuesta=await http.GetAsync(Url(ruta));
                    Assert.Equal(HttpStatusCode.Forbidden,respuesta.StatusCode);
                }
            }
        }
    }
}
