using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Choferes;

/// <summary>
/// Eliminación definitiva de un documento (FR-015c, FR-015d).
///
/// Es la única operación del módulo que borra de verdad: la fila y su archivo desaparecen y no se
/// pueden recuperar.
/// </summary>
public class EliminarDocumentoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Elimina_LaFila_YSuArchivo()
    {
        var chofer = await app.CrearChoferCompletoAsync(semilla: 93111222);
        var tipo = await app.CrearTipoDocumentacionAsync();

        var cliente = await app.CrearClienteAutenticadoAsync();

        var alta = await cliente.PostAsync(
            $"/api/choferes/{chofer.Id}/documentacion",
            AyudasDeDocumentacion.Formulario(
                tipo.Id,
                100,
                archivo: AyudasDeDocumentacion.Pdf()));

        var documento = await alta.Content.ReadFromJsonAsync<DocumentoLeido>();

        // Antes de borrar, el archivo se descarga.
        var descargaPrevia = await cliente.GetAsync($"/api/documentacion/{documento!.Id}/archivo");
        Assert.Equal(HttpStatusCode.OK, descargaPrevia.StatusCode);

        var respuesta = await cliente.DeleteAsync($"/api/documentacion/{documento.Id}");
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);

        // La fila no está.
        Assert.Null(await app.RecargarDocumentoAsync(documento.Id));

        // Y el archivo tampoco: la descarga ya no encuentra nada.
        var descargaPosterior = await cliente.GetAsync($"/api/documentacion/{documento.Id}/archivo");
        Assert.Equal(HttpStatusCode.NotFound, descargaPosterior.StatusCode);
    }

    /// <summary>
    /// Al eliminar el vigente de un tipo, el anterior vuelve a mandar y el estado del chofer cambia
    /// solo, sin que nadie actualice ninguna fila (FR-020a).
    /// </summary>
    [Fact]
    public async Task Al_EliminarElVigente_ElAnteriorVuelveAMandar()
    {
        var chofer = await app.CrearChoferCompletoAsync(semilla: 94111222);
        var tipo = await app.CrearTipoDocumentacionAsync();

        var cliente = await app.CrearClienteAutenticadoAsync();

        var altaVieja = await cliente.PostAsync(
            $"/api/choferes/{chofer.Id}/documentacion",
            AyudasDeDocumentacion.Formulario(tipo.Id, -10, numero: "VIEJO"));
        var viejo = await altaVieja.Content.ReadFromJsonAsync<DocumentoLeido>();

        var altaNueva = await cliente.PostAsync(
            $"/api/choferes/{chofer.Id}/documentacion",
            AyudasDeDocumentacion.Formulario(tipo.Id, 300, numero: "NUEVO"));
        var nuevo = await altaNueva.Content.ReadFromJsonAsync<DocumentoLeido>();

        // Con la renovación cargada, el chofer está en regla.
        var enRegla = await cliente.GetFromJsonAsync<ChoferConDocumentos>($"/api/choferes/{chofer.Id}");
        Assert.Equal("enRegla", enRegla!.EstadoDocumentacion);

        await cliente.DeleteAsync($"/api/documentacion/{nuevo!.Id}");

        // Sin la renovación, el vencido vuelve a mandar y el chofer figura vencido.
        var vencido = await cliente.GetFromJsonAsync<ChoferConDocumentos>($"/api/choferes/{chofer.Id}");

        Assert.Equal("vencida", vencido!.EstadoDocumentacion);

        var queQueda = Assert.Single(vencido.Documentos);
        Assert.Equal(viejo!.Id, queQueda.Id);
        Assert.True(queQueda.EsVigenteDelTipo);
    }

    [Fact]
    public async Task Responde_NoEncontrado_AlEliminarUnDocumentoInexistente()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.DeleteAsync("/api/documentacion/999999");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorLeido>();
        Assert.Equal("no_encontrado", error!.Codigo);
    }
}
