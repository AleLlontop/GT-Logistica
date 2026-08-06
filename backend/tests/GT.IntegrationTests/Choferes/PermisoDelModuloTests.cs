using System.Net;
using System.Net.Http.Json;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Usuarios;

namespace GT.IntegrationTests.Choferes;

/// <summary>
/// FR-027: <b>todos</b> los endpoints del módulo exigen sesión y el permiso
/// <c>choferes.gestionar</c>, sin excepción.
///
/// Este test recorre la superficie entera en vez de confiar en que cada grupo la haya declarado.
/// Un endpoint nuevo que se agregue fuera de un grupo con <c>RequireAuthorization</c> —el error
/// fácil de cometer— cae acá, incluida la descarga del escaneo, que es la que más importa porque
/// devuelve un dato personal sensible (FR-024, SC-011).
/// </summary>
public class PermisoDelModuloTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    public static TheoryData<string, string> RutasDelModulo() => new()
    {
        { "GET", "/api/choferes" },
        { "GET", "/api/choferes/1" },
        { "POST", "/api/choferes" },
        { "PUT", "/api/choferes/1" },
        { "DELETE", "/api/choferes/1" },
        { "POST", "/api/choferes/1/reactivacion" },
        { "POST", "/api/choferes/1/documentacion" },
        { "GET", "/api/transportistas" },
        { "GET", "/api/transportistas/1" },
        { "POST", "/api/transportistas" },
        { "PUT", "/api/transportistas/1" },
        { "DELETE", "/api/transportistas/1" },
        { "GET", "/api/tipos-documentacion" },
        { "POST", "/api/tipos-documentacion" },
        { "PUT", "/api/tipos-documentacion/1" },
        { "DELETE", "/api/tipos-documentacion/1" },
        { "PUT", "/api/documentacion/1" },
        { "DELETE", "/api/documentacion/1" },
        { "GET", "/api/documentacion/1/archivo" },
        { "GET", "/api/vencimientos" },
    };

    [Theory]
    [MemberData(nameof(RutasDelModulo))]
    public async Task Sin_Sesion_Responde401(string metodo, string ruta)
    {
        var anonimo = app.CrearCliente();

        var respuesta = await anonimo.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), ruta));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorLeido>();
        Assert.Equal("sesion_expirada", error!.Codigo);
    }

    [Theory]
    [MemberData(nameof(RutasDelModulo))]
    public async Task Con_SesionSinElPermiso_Responde403(string metodo, string ruta)
    {
        // Gerencia tiene cuenta y entra al sistema; este módulo no es suyo.
        var cliente = await app.CrearClienteComoAsync(
            $"gerencia{Guid.NewGuid():N}"[..20],
            CodigosRol.Gerencia);

        var respuesta = await cliente.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), ruta));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorLeido>();
        Assert.Equal("sin_permiso", error!.Codigo);
    }
}
