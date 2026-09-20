using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Viajes;

namespace GT.IntegrationTests.Reportes;

/// <summary>
/// Los dos caminos de error que sólo se ven contra la aplicación entera: el <c>400</c> del formato y
/// el <c>500</c> del motor que falla (FR-017).
///
/// <c>RespuestasDeReporteTests</c> prueba la <b>traducción</b> a HTTP; esto prueba que el fallo llegue
/// hasta ella. Es el único lugar donde el <c>500</c> se ejerce de verdad.
///
/// Van sobre <c>/api/viajes/reporte</c> y valen para los cinco: los cuatro cuerpos del contrato los
/// arma el mismo traductor.
/// </summary>
public class ErroresDeReporteTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    // ── 400 formato_invalido ────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Sin <c>formato</c>, con uno vacío y con uno desconocido: los tres son el mismo rechazo, y
    /// <b>ninguno devuelve archivo</b>.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("&formato=")]
    [InlineData("&formato=csv")]
    [InlineData("&formato=PDF")]
    public async Task SinFormatoValido_Responde400YNingunArchivo(string parametro)
    {
        var cliente = await app.CrearClienteAsync();
        await app.CrearViajeAsync(cliente.Id);
        var administrador = await app.CrearClienteAutenticadoAsync();

        var respuesta = await administrador.GetAsync(
            $"/api/viajes/reporte?clienteId={cliente.Id}{parametro}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorDeReporte>();

        Assert.Equal("formato_invalido", error!.Codigo);
        Assert.Equal("El formato del reporte tiene que ser PDF o Excel.", error.Mensaje);

        // Ningún archivo: la respuesta es JSON, no un PDF ni un `.xlsx`.
        Assert.Equal("application/json", respuesta.Content.Headers.ContentType?.MediaType);
    }

    // ── 500 reporte_no_generado ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Con el armador de PDF fallando, la respuesta es el <c>500</c> del contrato con su texto en
    /// español: <b>no una excepción sin manejar ni un <c>200</c> con cero bytes</b>.
    /// </summary>
    [Fact]
    public async Task ConElArmadorFallando_Responde500ConElTextoDelContrato()
    {
        var cliente = await app.CrearClienteAsync();
        await app.CrearViajeAsync(cliente.Id);

        using var conArmadorRoto = app.ConArmadorDePdfQueFalla();
        var administrador = await conArmadorRoto.ClienteAdministradorAsync();

        var respuesta = await administrador.GetAsync(
            $"/api/viajes/reporte?formato=pdf&clienteId={cliente.Id}");

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorDeReporte>();

        Assert.Equal("reporte_no_generado", error!.Codigo);
        Assert.Equal(
            "No pudimos generar el reporte. Volvé a intentar en unos minutos.",
            error.Mensaje);
    }

    /// <summary>
    /// **Un rechazo no es un callejón sin salida** (FR-017): con el armador de PDF roto, el mismo
    /// filtro en Excel sigue funcionando. La operación se puede reintentar sin recargar nada.
    /// </summary>
    [Fact]
    public async Task ConElArmadorDePdfRoto_ElExcelDelMismoFiltroSigueAndando()
    {
        var cliente = await app.CrearClienteAsync();
        await app.CrearViajeAsync(cliente.Id);

        using var conArmadorRoto = app.ConArmadorDePdfQueFalla();
        var administrador = await conArmadorRoto.ClienteAdministradorAsync();

        var fallida = await administrador.GetAsync(
            $"/api/viajes/reporte?formato=pdf&clienteId={cliente.Id}");

        var siguiente = await administrador.GetAsync(
            $"/api/viajes/reporte?formato=excel&clienteId={cliente.Id}");

        Assert.Equal(HttpStatusCode.InternalServerError, fallida.StatusCode);
        Assert.Equal(HttpStatusCode.OK, siguiente.StatusCode);
    }
}
