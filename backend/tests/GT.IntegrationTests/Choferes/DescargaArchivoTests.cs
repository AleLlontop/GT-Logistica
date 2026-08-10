using System.Net;
using System.Net.Http.Json;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Usuarios;

namespace GT.IntegrationTests.Choferes;

/// <summary>
/// FR-024 y SC-011: el escaneo se sirve por endpoint autorizado, nunca como contenido estático.
///
/// Un psicofísico o una licencia son datos personales sensibles: conocer la dirección no puede
/// alcanzar para verlos (research §3, Principio V).
/// </summary>
public class DescargaArchivoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private async Task<int> CrearDocumentoConArchivoAsync(int semilla)
    {
        var chofer = await app.CrearChoferCompletoAsync(semilla);
        var tipo = await app.CrearTipoDocumentacionAsync();

        var cliente = await app.CrearClienteAutenticadoAsync();

        var alta = await cliente.PostAsync(
            $"/api/choferes/{chofer.Id}/documentacion",
            AyudasDeDocumentacion.Formulario(
                tipo.Id,
                100,
                archivo: AyudasDeDocumentacion.Pdf("secreto")));

        var documento = await alta.Content.ReadFromJsonAsync<DocumentoLeido>();

        return documento!.Id;
    }

    [Fact]
    public async Task Sin_Sesion_Responde401()
    {
        var documentoId = await CrearDocumentoConArchivoAsync(95111222);

        var anonimo = app.CrearCliente();

        var respuesta = await anonimo.GetAsync($"/api/documentacion/{documentoId}/archivo");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorLeido>();
        Assert.Equal("sesion_expirada", error!.Codigo);
    }

    /// <summary>
    /// Con sesión válida pero sin <c>choferes.gestionar</c>, tampoco. Gerencia tiene cuenta y entra
    /// al sistema; este módulo no es suyo.
    /// </summary>
    [Fact]
    public async Task Con_SesionSinElPermiso_Responde403()
    {
        var documentoId = await CrearDocumentoConArchivoAsync(96111222);

        var cliente = await app.CrearClienteComoAsync("gerencia.descarga", CodigosRol.Gerencia);

        var respuesta = await cliente.GetAsync($"/api/documentacion/{documentoId}/archivo");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorLeido>();
        Assert.Equal("sin_permiso", error!.Codigo);
    }

    /// <summary>Un usuario de Tráfico sí puede: el permiso es suyo (FR-027).</summary>
    [Fact]
    public async Task Con_ElPermiso_DescargaElArchivo()
    {
        var documentoId = await CrearDocumentoConArchivoAsync(97111222);

        var cliente = await app.CrearClienteComoAsync("trafico.descarga", CodigosRol.Trafico);

        var respuesta = await cliente.GetAsync($"/api/documentacion/{documentoId}/archivo");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("application/pdf", respuesta.Content.Headers.ContentType?.MediaType);

        var contenido = await respuesta.Content.ReadAsByteArrayAsync();
        Assert.Equal(AyudasDeDocumentacion.Pdf("secreto"), contenido);
    }

    /// <summary>
    /// El archivo se sirve <b>en línea</b>: quien abre un documento lo quiere ver en el navegador, no
    /// bajarlo y abrirlo a mano. Es la cabecera la que decide, no el enlace del frontend.
    ///
    /// El nombre viaja igual, para que "Guardar como" siga proponiendo el original, y va
    /// <c>nosniff</c> porque servir contenido en línea desde el propio origen no puede depender de
    /// que el navegador adivine el tipo.
    /// </summary>
    [Fact]
    public async Task El_Archivo_SeSirveEnLinea_ConSuNombre()
    {
        var documentoId = await CrearDocumentoConArchivoAsync(99111222);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync($"/api/documentacion/{documentoId}/archivo");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var disposicion = respuesta.Content.Headers.ContentDisposition;
        Assert.Equal("inline", disposicion!.DispositionType);
        Assert.Contains("escaneo.pdf", $"{disposicion.FileName} {disposicion.FileNameStar}");

        Assert.Equal("nosniff", Assert.Single(respuesta.Headers.GetValues("X-Content-Type-Options")));
    }

    /// <summary>Un documento sin adjunto se comunica igual que uno inexistente: no hay nada que dar.</summary>
    [Fact]
    public async Task Sin_Adjunto_RespondeNoEncontrado()
    {
        var chofer = await app.CrearChoferCompletoAsync(semilla: 98111222);
        var tipo = await app.CrearTipoDocumentacionAsync();

        var cliente = await app.CrearClienteAutenticadoAsync();

        var alta = await cliente.PostAsync(
            $"/api/choferes/{chofer.Id}/documentacion",
            AyudasDeDocumentacion.Formulario(tipo.Id, 100));
        var documento = await alta.Content.ReadFromJsonAsync<DocumentoLeido>();

        var respuesta = await cliente.GetAsync($"/api/documentacion/{documento!.Id}/archivo");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
