using System.Net;
using System.Text.Json;
using GT.Domain.Choferes;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;

namespace GT.IntegrationTests.Reportes;

public class RutasVencimientosAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task PermisosPorPanel_SituacionActual_TotalCompleto_NoPeriodoHistorico()
    {
        var cliente = await app.CrearClienteAsync();
        var hoy = FechaHoyArgentina.Desde(TimeProvider.System.GetUtcNow());
        for(var i=0;i<21;i++)await app.CrearFacturaAsync(cliente.Id,fecha:hoy,vencimientoPago:hoy.AddDays(1),neto:1m);
        string[] paneles=["choferes","flota","facturas"];
        string[] fuentes=[CodigosPermiso.ChoferesVencimientosConsultar,CodigosPermiso.FlotaVencimientosConsultar,CodigosPermiso.FacturacionConsultar];
        for(var indice=0;indice<3;indice++)
        foreach(var tieneBase in new[]{false,true})
        {
            var nombre="rv_"+Guid.NewGuid().ToString("N")[..10];
            await app.CrearUsuarioConPermisosAsync(nombre,"Reportes.12345678",tieneBase?[CodigosPermiso.ReportesConsultar,fuentes[indice]]:[fuentes[indice]]);
            using var http=await app.CrearClienteAutenticadoAsync(nombre,"Reportes.12345678");
            for(var j=0;j<3;j++)
            {
                using var respuesta=await http.GetAsync("/api/reportes/vencimientos/"+paneles[j]);
                Assert.Equal(tieneBase && indice==j?HttpStatusCode.OK:HttpStatusCode.Forbidden,respuesta.StatusCode);
                if(!tieneBase || indice!=j)continue;
                using var json=JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
                Assert.Equal("situacionActual",json.RootElement.GetProperty("contexto").GetProperty("criterioTemporal").GetString());
                Assert.Equal(JsonValueKind.Null,json.RootElement.GetProperty("contexto").GetProperty("desde").ValueKind);
                if(j==2)
                {
                    Assert.Equal(21,json.RootElement.GetProperty("detalle").GetProperty("total").GetInt32());
                    Assert.Equal(20,json.RootElement.GetProperty("detalle").GetProperty("items").GetArrayLength());
                    Assert.Equal("25.41",json.RootElement.GetProperty("importeTotal").GetString());
                }
            }
        }
        using var admin=await app.CrearClienteAutenticadoAsync();
        using var segunda=await admin.GetAsync("/api/reportes/vencimientos/facturas?pagina=2");
        using var pagina=JsonDocument.Parse(await segunda.Content.ReadAsStringAsync());
        Assert.Single(pagina.RootElement.GetProperty("detalle").GetProperty("items").EnumerateArray());
        Assert.Equal("25.41",pagina.RootElement.GetProperty("importeTotal").GetString());
        using var periodo=await admin.GetAsync("/api/reportes/vencimientos/choferes?desde=2026-10-01&hasta=2026-10-31");
        Assert.Equal(HttpStatusCode.BadRequest,periodo.StatusCode);
        var baseNombre="rvbase_"+Guid.NewGuid().ToString("N")[..8];
        await app.CrearUsuarioConPermisosAsync(baseNombre,"Reportes.12345678",CodigosPermiso.ReportesConsultar);
        using var soloBase=await app.CrearClienteAutenticadoAsync(baseNombre,"Reportes.12345678");
        foreach(var panel in paneles)
        {
            using var respuesta=await soloBase.GetAsync("/api/reportes/vencimientos/"+panel);
            Assert.Equal(HttpStatusCode.Forbidden,respuesta.StatusCode);
        }
    }
}
