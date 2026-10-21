using System.Net;
using System.Net.Http.Json;
using GT.Api.Autenticacion;
using GT.Api.Autorizacion;
using GT.Application.Autenticacion;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Facturacion;
using GT.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Reportes;

public class PermisosAnalisisTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Theory]
    [InlineData(false,false,false)] [InlineData(true,false,false)]
    [InlineData(false,true,false)] [InlineData(true,true,false)]
    [InlineData(false,false,true)] [InlineData(true,false,true)]
    [InlineData(false,true,true)] [InlineData(true,true,true)]
    public async Task BaseFuenteYEmisionSonIndependientes_YLegacyNoPideBase(bool tieneBase, bool tieneFuente, bool emite)
    {
        var permisos = new List<string>();
        if(tieneBase) permisos.Add(CodigosPermiso.ReportesConsultar);
        if(tieneFuente) permisos.Add(CodigosPermiso.ViajesConsultar);
        if(emite) permisos.Add(CodigosPermiso.ReportesEmitir);
        var nombre="analisis_"+Guid.NewGuid().ToString("N")[..10];
        var usuario=await app.CrearUsuarioConPermisosAsync(nombre,"Analisis.12345678",permisos.ToArray());
        using var scope=app.Services.CreateScope();
        var autorizacion=scope.ServiceProvider.GetRequiredService<IAuthorizationService>();
        var principal=ClaimsSesion.Construir(usuario);
        Assert.Equal(tieneBase,(await autorizacion.AuthorizeAsync(principal,PoliticasAutorizacion.Para(CodigosPermiso.ReportesConsultar))).Succeeded);
        Assert.Equal(tieneBase&&tieneFuente,(await autorizacion.AuthorizeAsync(principal,PoliticasAutorizacion.ParaTodos(CodigosPermiso.ReportesConsultar,CodigosPermiso.ViajesConsultar))).Succeeded);
        Assert.Equal(tieneBase&&tieneFuente&&emite,(await autorizacion.AuthorizeAsync(principal,PoliticasAutorizacion.ParaTodos(CodigosPermiso.ReportesConsultar,CodigosPermiso.ViajesConsultar,CodigosPermiso.ReportesEmitir))).Succeeded);
        using var cliente=await app.CrearClienteAutenticadoAsync(nombre,"Analisis.12345678");
        var sesion=await cliente.GetFromJsonAsync<SesionResponse>("/api/auth/sesion");
        Assert.Equal(permisos.Order(),sesion!.Permisos.Order());
        foreach(var ruta in new[]{"/api/reportes/viajes?desde=2026-10-01&hasta=2026-10-31", "/api/reportes/viajes/grupos?desde=2026-10-01&hasta=2026-10-31&dimension=estado", "/api/reportes/viajes/referencias/clientes", "/api/reportes/viajes/referencias/transportistas"})
        {
            using var analisis = await cliente.GetAsync(ruta);
            Assert.Equal(tieneBase && tieneFuente ? HttpStatusCode.ServiceUnavailable : HttpStatusCode.Forbidden, analisis.StatusCode);
            if (!(tieneBase && tieneFuente))
            {
                var cuerpo = await analisis.Content.ReadAsStringAsync();
                Assert.Contains("No tenés permiso para consultar o emitir este reporte.", System.Text.Json.JsonDocument.Parse(cuerpo).RootElement.GetProperty("mensaje").GetString());
            }
        }
        using var legacy=await cliente.GetAsync("/api/viajes/reporte?formato=excel");
        Assert.Equal(tieneFuente&&emite?HttpStatusCode.OK:HttpStatusCode.Forbidden,legacy.StatusCode);
    }

    [Fact]
    public async Task PermisoRevocadoSeRevalidaSinMantenerLaCookieComoAutoridad()
    {
        var nombre="rev_"+Guid.NewGuid().ToString("N")[..10];
        var usuario=await app.CrearUsuarioConPermisosAsync(nombre,"Analisis.12345678",CodigosPermiso.ReportesConsultar);
        using var cliente=await app.CrearClienteAutenticadoAsync(nombre,"Analisis.12345678");
        Assert.Contains(CodigosPermiso.ReportesConsultar,(await cliente.GetFromJsonAsync<SesionResponse>("/api/auth/sesion"))!.Permisos);
        await app.EnLaBaseAsync(async db =>
        {
            var rol=await db.Roles.Include(r=>r.Permisos).SingleAsync(r=>r.Id==usuario.Roles.Single().Id);
            rol.Permisos.Clear(); await db.SaveChangesAsync();
        });
        Assert.DoesNotContain(CodigosPermiso.ReportesConsultar,(await cliente.GetFromJsonAsync<SesionResponse>("/api/auth/sesion"))!.Permisos);
    }
}
