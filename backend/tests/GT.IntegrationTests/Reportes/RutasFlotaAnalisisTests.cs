using System.Net;
using System.Text.Json;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Flota;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Liquidaciones;

namespace GT.IntegrationTests.Reportes;

public class RutasFlotaAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task PermisoPropioNoGestionaFlota_ActivoAusenteIncluyeBajas_YLookupsMinimos()
    {
        var propietario = await app.TransportistaExternoAsync();
        var tipo = await app.CrearTipoVehiculoAsync();
        var a = await app.CrearVehiculoAsync(tipo.Id,propietario.Id);
        var b = await app.CrearVehiculoAsync(tipo.Id,propietario.Id,activo:false);
        foreach(var permisos in new[]{Array.Empty<string>(),new[]{CodigosPermiso.ReportesConsultar},new[]{CodigosPermiso.ReportesFlotaConsultar},new[]{CodigosPermiso.ReportesConsultar,CodigosPermiso.ReportesFlotaConsultar}})
        {
            var nombre="rflo_"+Guid.NewGuid().ToString("N")[..10];
            await app.CrearUsuarioConPermisosAsync(nombre,"Reportes.12345678",permisos);
            using var http=await app.CrearClienteAutenticadoAsync(nombre,"Reportes.12345678");
            foreach(var ruta in new[]{"/api/reportes/flota","/api/reportes/flota/grupos","/api/reportes/flota/referencias/vehiculos","/api/reportes/flota/referencias/tipos","/api/reportes/flota/referencias/transportistas"})
            {
                using var respuesta=await http.GetAsync(ruta+$"?desde=2026-10-01&hasta=2026-10-31&transportistaId={propietario.Id}&dimension=tipo");
                Assert.Equal(permisos.Length==2?HttpStatusCode.OK:HttpStatusCode.Forbidden,respuesta.StatusCode);
                if(permisos.Length!=2 || ruta!="/api/reportes/flota")continue;
                using var json=JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
                Assert.Equal(2,json.RootElement.GetProperty("detalle").GetProperty("total").GetInt32());
                Assert.Equal(0,json.RootElement.GetProperty("resumen").GetProperty("totalViajesConVehiculo").GetInt64());
                Assert.Contains(json.RootElement.GetProperty("detalle").GetProperty("items").EnumerateArray(),v=>!v.GetProperty("activo").GetBoolean());
            }
            using var gestion=await http.GetAsync("/api/flota/vehiculos");
            Assert.Equal(HttpStatusCode.Forbidden,gestion.StatusCode);
        }
        using var admin=await app.CrearClienteAutenticadoAsync();
        using var bajas=await admin.GetAsync($"/api/reportes/flota?desde=2026-10-01&hasta=2026-10-31&transportistaId={propietario.Id}&activo=false");
        Assert.Equal(HttpStatusCode.OK,bajas.StatusCode);
        using var datos=JsonDocument.Parse(await bajas.Content.ReadAsStringAsync());
        Assert.Equal(b.Id,datos.RootElement.GetProperty("detalle").GetProperty("items")[0].GetProperty("id").GetInt32());
        using var invalido=await admin.GetAsync("/api/reportes/flota?desde=2026-10-01&hasta=2026-10-31&activo=incorrecto");
        Assert.Equal(HttpStatusCode.BadRequest,invalido.StatusCode);
        using var referencias=await admin.GetAsync("/api/reportes/flota/referencias/vehiculos?busqueda="+b.Patente);
        using var lookup=JsonDocument.Parse(await referencias.Content.ReadAsStringAsync());
        var fila=Assert.Single(lookup.RootElement.GetProperty("items").EnumerateArray());
        Assert.False(fila.GetProperty("activo").GetBoolean());
        Assert.Equal(new[]{"activo","id","nombre"},fila.EnumerateObject().Select(p=>p.Name).Order());
    }
}
