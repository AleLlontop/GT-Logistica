using System.Net;
using System.Net.Http.Json;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Usuarios;

namespace GT.IntegrationTests.Choferes;

/// <summary>
/// FR-027: <b>todos</b> los endpoints del módulo exigen sesión, y todos menos el panel de
/// vencimientos exigen <c>choferes.gestionar</c>.
///
/// Este test recorre la superficie entera en vez de confiar en que cada grupo la haya declarado.
/// Un endpoint nuevo que se agregue fuera de un grupo con <c>RequireAuthorization</c> —el error
/// fácil de cometer— cae acá, incluida la descarga del escaneo, que es la que más importa porque
/// devuelve un dato personal sensible (FR-024, SC-011).
///
/// La única excepción es <c>GET /api/vencimientos</c>, que desde que Gerencia mira los vencimientos
/// va bajo <c>choferes.vencimientos.consultar</c>. Sigue en la lista de las que exigen sesión: lo
/// que cambia es cuál permiso, no que haya uno.
/// </summary>
public class PermisoDelModuloTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private const string PanelDeVencimientos = "/api/vencimientos";

    private static readonly (string Metodo, string Ruta)[] Rutas =
    [
        ("GET", "/api/choferes"),
        ("GET", "/api/choferes/1"),
        ("POST", "/api/choferes"),
        ("PUT", "/api/choferes/1"),
        ("DELETE", "/api/choferes/1"),
        ("POST", "/api/choferes/1/reactivacion"),
        ("POST", "/api/choferes/1/documentacion"),
        ("GET", "/api/transportistas"),
        ("GET", "/api/transportistas/1"),
        ("POST", "/api/transportistas"),
        ("PUT", "/api/transportistas/1"),
        ("DELETE", "/api/transportistas/1"),
        ("GET", "/api/tipos-documentacion"),
        ("POST", "/api/tipos-documentacion"),
        ("PUT", "/api/tipos-documentacion/1"),
        ("DELETE", "/api/tipos-documentacion/1"),
        ("PUT", "/api/documentacion/1"),
        ("DELETE", "/api/documentacion/1"),
        ("GET", "/api/documentacion/1/archivo"),
        ("GET", PanelDeVencimientos),
    ];

    /// <summary>La superficie entera del módulo: todas exigen sesión.</summary>
    public static TheoryData<string, string> RutasDelModulo() => ComoTheoryData(Rutas);

    /// <summary>
    /// Las que exigen <c>choferes.gestionar</c>: todas menos el panel de vencimientos, que tiene
    /// permiso propio.
    /// </summary>
    public static TheoryData<string, string> RutasQueExigenGestionar() =>
        ComoTheoryData(Rutas.Where(r => r.Ruta != PanelDeVencimientos));

    private static TheoryData<string, string> ComoTheoryData(
        IEnumerable<(string Metodo, string Ruta)> rutas)
    {
        var datos = new TheoryData<string, string>();

        foreach (var (metodo, ruta) in rutas)
        {
            datos.Add(metodo, ruta);
        }

        return datos;
    }

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
    [MemberData(nameof(RutasQueExigenGestionar))]
    public async Task Con_SesionSinElPermiso_Responde403(string metodo, string ruta)
    {
        // Gerencia tiene cuenta y entra al sistema; operar este módulo no es suyo.
        var cliente = await app.CrearClienteComoAsync(
            $"gerencia{Guid.NewGuid():N}"[..20],
            CodigosRol.Gerencia);

        var respuesta = await cliente.SendAsync(new HttpRequestMessage(new HttpMethod(metodo), ruta));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorLeido>();
        Assert.Equal("sin_permiso", error!.Codigo);
    }

    /// <summary>
    /// Gerencia <b>sí</b> llega al panel de vencimientos: es lo único del módulo que ve, y lo ve sin
    /// poder tocar el padrón ni descargar un escaneo —los dos casos quedan cubiertos por el 403 de
    /// arriba—.
    /// </summary>
    [Fact]
    public async Task Gerencia_LlegaAlPanelDeVencimientos()
    {
        var cliente = await app.CrearClienteComoAsync(
            $"gerencia{Guid.NewGuid():N}"[..20],
            CodigosRol.Gerencia);

        var respuesta = await cliente.GetAsync("/api/vencimientos");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }
}
