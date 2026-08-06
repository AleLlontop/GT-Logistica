using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Usuarios;

namespace GT.IntegrationTests.Choferes;

/// <summary>
/// FR-015e: la carga del adjunto es todo o nada.
///
/// Es el único requisito del módulo sin escenario de aceptación, porque describe una falla de
/// almacenamiento que nadie puede provocar desde la pantalla. Se verifica acá, sustituyendo el
/// almacén por uno que nunca escribe (plan.md, Constitution Check).
///
/// Lo que se prueba no es que falle: es <b>qué queda</b> cuando falla. El estado prohibido es una
/// fila que dice tener adjunto y no lo tiene (research §10).
/// </summary>
public class DocumentacionAtomicidadTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Si_ElArchivoNoSeGuarda_ElDocumentoNoSeCrea()
    {
        var chofer = await app.CrearChoferCompletoAsync(semilla: 91111222);
        var tipo = await app.CrearTipoDocumentacionAsync();

        var fabrica = app.ConAlmacenQueFalla();
        var cliente = await fabrica.CrearClienteAdministradorAsync();

        var respuesta = await cliente.PostAsync(
            $"/api/choferes/{chofer.Id}/documentacion",
            AyudasDeDocumentacion.Formulario(
                tipo.Id,
                100,
                archivo: AyudasDeDocumentacion.Pdf()));

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorLeido>();
        Assert.Equal("archivo_no_guardado", error!.Codigo);
        Assert.Equal(
            "No pudimos guardar el archivo, así que no se guardó nada. Volvé a intentar; los datos " +
            "que cargaste se conservan.",
            error.Mensaje);

        // Lo que importa: no quedó una fila a medias.
        Assert.Equal(0, await app.ContarDocumentosDelChoferAsync(chofer.Id));
    }

    /// <summary>
    /// Corregir con un archivo de reemplazo que no llega a guardarse deja el documento exactamente
    /// como estaba, con su adjunto anterior intacto (FR-015e).
    /// </summary>
    [Fact]
    public async Task Si_ElReemplazoNoSeGuarda_ElDocumentoQuedaComoEstaba()
    {
        var chofer = await app.CrearChoferCompletoAsync(semilla: 92111222);
        var tipo = await app.CrearTipoDocumentacionAsync();

        var cliente = await app.CrearClienteAutenticadoAsync();

        var alta = await cliente.PostAsync(
            $"/api/choferes/{chofer.Id}/documentacion",
            AyudasDeDocumentacion.Formulario(
                tipo.Id,
                100,
                numero: "ORIGINAL",
                archivo: AyudasDeDocumentacion.Pdf("original"),
                nombreDeArchivo: "original.pdf"));

        var documento = await alta.Content.ReadFromJsonAsync<DocumentoLeido>();
        var antes = await app.RecargarDocumentoAsync(documento!.Id);

        // Ahora el almacén deja de funcionar y se intenta reemplazar el adjunto.
        var fabricaRota = app.ConAlmacenQueFalla();
        var clienteRoto = await fabricaRota.CrearClienteAdministradorAsync();

        var respuesta = await clienteRoto.PutAsync(
            $"/api/documentacion/{documento.Id}",
            AyudasDeDocumentacion.Formulario(
                tipo.Id,
                500,
                numero: "CORREGIDO",
                archivo: AyudasDeDocumentacion.Pdf("reemplazo"),
                nombreDeArchivo: "reemplazo.pdf"));

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorLeido>();
        Assert.Equal("archivo_no_guardado", error!.Codigo);

        // Ni los datos ni el adjunto se movieron: la corrección quedó sin efecto.
        var despues = await app.RecargarDocumentoAsync(documento.Id);

        Assert.NotNull(despues);
        Assert.Equal("ORIGINAL", despues.Numero);
        Assert.Equal(antes!.FechaVencimiento, despues.FechaVencimiento);
        Assert.Equal(antes.ArchivoRuta, despues.ArchivoRuta);
        Assert.Equal("original.pdf", despues.ArchivoNombre);
    }
}
