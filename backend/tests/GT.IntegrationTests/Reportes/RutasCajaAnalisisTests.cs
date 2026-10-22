using System.Net;
using System.Text.Json;
using GT.Domain.Caja;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Caja;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Reportes;

public class RutasCajaAnalisisTests(AplicacionAnalisisDePrueba app) : IClassFixture<AplicacionAnalisisDePrueba>
{
    [Fact]
    public async Task PermisoCompuesto_ImporteFirmado_LookupMinimoSinGestion()
    {
        var responsable = await app.UsuarioAsync();
        var caja = await app.CrearCajaAsync(responsable.Id);
        await app.CrearMovimientoAsync(caja.Id, responsable.Id, tipo: TipoMovimientoCaja.Egreso, importe: 0.01m, fecha: new(2026,10,18,12,0,0,DateTimeKind.Utc));
        foreach(var permisos in new[]{Array.Empty<string>(), new[]{CodigosPermiso.ReportesConsultar}, new[]{CodigosPermiso.CajaConsultar}, new[]{CodigosPermiso.ReportesConsultar,CodigosPermiso.CajaConsultar}})
        {
            var nombre = "rc_" + Guid.NewGuid().ToString("N")[..10];
            await app.CrearUsuarioConPermisosAsync(nombre,"Reportes.12345678",permisos);
            using var http = await app.CrearClienteAutenticadoAsync(nombre,"Reportes.12345678");
            foreach(var ruta in new[]{"/api/reportes/caja", "/api/reportes/caja/grupos", "/api/reportes/caja/referencias/cajas"})
            {
                using var respuesta = await http.GetAsync(ruta + $"?desde=2026-10-01&hasta=2026-10-31&cajaId={caja.Id}&dimension=tipo");
                Assert.Equal(permisos.Length==2 ? HttpStatusCode.OK : HttpStatusCode.Forbidden,respuesta.StatusCode);
                if(permisos.Length!=2) continue;
                using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
                if(ruta=="/api/reportes/caja")
                {
                    Assert.Equal("-0.01",json.RootElement.GetProperty("resumen").GetProperty("neto").GetString());
                    Assert.Equal("fechaMovimientoArgentina",json.RootElement.GetProperty("contexto").GetProperty("criterioTemporal").GetString());
                    Assert.Equal(1,json.RootElement.GetProperty("detalle").GetProperty("total").GetInt32());
                }
                if(ruta.Contains("referencias"))
                {
                    var fila=json.RootElement.GetProperty("items").EnumerateArray().Single(f=>f.GetProperty("id").GetInt32()==caja.Id);
                    Assert.Contains(responsable.Username,fila.GetProperty("nombre").GetString());
                    Assert.Equal(new[]{"activo","id","nombre"},fila.EnumerateObject().Select(p=>p.Name).Order());
                }
            }
            using var bancos=await http.GetAsync("/api/pagos/bancos");
            Assert.Equal(HttpStatusCode.Forbidden,bancos.StatusCode);
        }
        using var admin=await app.CrearClienteAutenticadoAsync();
        using var invalido=await admin.GetAsync("/api/reportes/caja?desde=2026-10-01&hasta=2026-10-31&cajaId=-1");
        Assert.Equal(HttpStatusCode.BadRequest,invalido.StatusCode);
    }
}
