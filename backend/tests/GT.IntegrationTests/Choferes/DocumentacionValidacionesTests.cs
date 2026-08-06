using System.Net;
using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Choferes;

/// <summary>Validaciones del alta de documentación (FR-016, FR-015a, US3 esc. 2).</summary>
public class DocumentacionValidacionesTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Rechaza_VencimientoAnteriorALaEmision()
    {
        var chofer = await app.CrearChoferCompletoAsync(semilla: 86111222);
        var tipo = await app.CrearTipoDocumentacionAsync();
        var hoy = FechaHoyArgentina.Hoy();

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsync(
            $"/api/choferes/{chofer.Id}/documentacion",
            AyudasDeDocumentacion.Formulario(
                tipo.Id,
                0,
                fechaEmision: hoy,
                fechaVencimiento: hoy.AddDays(-1)));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorLeido>();
        Assert.Equal("vencimiento_anterior_a_emision", error!.Codigo);
        Assert.Equal("La fecha de vencimiento tiene que ser posterior a la de emisión.", error.Mensaje);
        Assert.Equal("fechaVencimiento", error.Campo);
    }

    /// <summary>Igual tampoco sirve: FR-016 pide posterior, no "no anterior".</summary>
    [Fact]
    public async Task Rechaza_VencimientoIgualALaEmision()
    {
        var chofer = await app.CrearChoferCompletoAsync(semilla: 87111222);
        var tipo = await app.CrearTipoDocumentacionAsync();
        var hoy = FechaHoyArgentina.Hoy();

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsync(
            $"/api/choferes/{chofer.Id}/documentacion",
            AyudasDeDocumentacion.Formulario(
                tipo.Id,
                0,
                fechaEmision: hoy,
                fechaVencimiento: hoy));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorLeido>();
        Assert.Equal("vencimiento_anterior_a_emision", error!.Codigo);
    }

    [Fact]
    public async Task Rechaza_UnTipoDadoDeBaja()
    {
        var chofer = await app.CrearChoferCompletoAsync(semilla: 88111222);
        var tipo = await app.CrearTipoDocumentacionAsync(nombre: "Tipo inactivo", activo: false);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsync(
            $"/api/choferes/{chofer.Id}/documentacion",
            AyudasDeDocumentacion.Formulario(tipo.Id, 100));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorLeido>();
        Assert.Equal("tipo_inexistente", error!.Codigo);
        Assert.Equal("documentacionTipoId", error.Campo);
    }

    [Fact]
    public async Task Rechaza_UnTipoInexistente()
    {
        var chofer = await app.CrearChoferCompletoAsync(semilla: 89111222);
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsync(
            $"/api/choferes/{chofer.Id}/documentacion",
            AyudasDeDocumentacion.Formulario(999999, 100));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorLeido>();
        Assert.Equal("tipo_inexistente", error!.Codigo);
    }

    /// <summary>
    /// El caso que justifica validar por firma: el archivo se llama <c>.pdf</c> y no lo es
    /// (FR-015a). El documento tampoco se crea: nada de guardar la fila y perder el adjunto.
    /// </summary>
    [Fact]
    public async Task Rechaza_UnArchivoQueDiceSerPdfYNoLoEs_YNoCreaElDocumento()
    {
        var chofer = await app.CrearChoferCompletoAsync(semilla: 90111222);
        var tipo = await app.CrearTipoDocumentacionAsync();

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsync(
            $"/api/choferes/{chofer.Id}/documentacion",
            AyudasDeDocumentacion.Formulario(
                tipo.Id,
                100,
                archivo: AyudasDeDocumentacion.NoEsUnPdf(),
                nombreDeArchivo: "en-realidad-es-un-ejecutable.pdf"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorLeido>();
        Assert.Equal("archivo_no_admitido", error!.Codigo);
        Assert.Equal("El archivo tiene que ser un PDF, JPG o PNG de hasta 10 MB.", error.Mensaje);
        Assert.Equal("archivo", error.Campo);

        var documentos = await app.ContarDocumentosDelChoferAsync(chofer.Id);
        Assert.Equal(0, documentos);
    }

    [Fact]
    public async Task Responde_NoEncontrado_SiElChoferNoExiste()
    {
        var tipo = await app.CrearTipoDocumentacionAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsync(
            "/api/choferes/999999/documentacion",
            AyudasDeDocumentacion.Formulario(tipo.Id, 100));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorLeido>();
        Assert.Equal("no_encontrado", error!.Codigo);
    }
}
