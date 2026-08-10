using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// El adjunto de un documento de la flota (FR-016a, FR-025, FR-038, US3 esc. 8).
///
/// El archivo es <b>opcional</b>, se limita a PDF/JPG/PNG de hasta 10 MB y el tipo se decide por la
/// <b>firma</b> del archivo, no por la extensión ni por el <c>Content-Type</c> declarado. Reutiliza el
/// validador del Módulo 3 sin modificarlo (research §2).
/// </summary>
public class ArchivoAdjuntoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    /// <summary>FR-016a: sin archivo el documento es válido y el estado del vehículo no cambia.</summary>
    [Fact]
    public async Task Acepta_UnDocumentoSinArchivo_YNoAlteraElEstadoDelVehiculo()
    {
        var (vehiculoId, tipoId) = await PrepararAsync("sin archivo");
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsync(
            $"/api/flota/vehiculos/{vehiculoId}/documentacion",
            AyudasDeDocumentacion.Formulario(tipoId, diasHastaVencimiento: 300));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var documento = await respuesta.Content.ReadFromJsonAsync<DocumentoVehiculoLeido>();
        Assert.False(documento!.TieneArchivo);
        Assert.Null(documento.ArchivoNombre);

        var ficha = await cliente.GetFromJsonAsync<VehiculoDetalleLeido>(
            $"/api/flota/vehiculos/{vehiculoId}");
        Assert.Equal("enRegla", ficha!.EstadoDocumentacion);
    }

    [Fact]
    public async Task Acepta_UnPdfYLoDevuelveParaDescargar()
    {
        var (vehiculoId, tipoId) = await PrepararAsync("con PDF");
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsync(
            $"/api/flota/vehiculos/{vehiculoId}/documentacion",
            AyudasDeDocumentacion.Formulario(
                tipoId,
                diasHastaVencimiento: 300,
                archivo: AyudasDeDocumentacion.Pdf(),
                nombreDeArchivo: "poliza.pdf"));

        var documento = await respuesta.Content.ReadFromJsonAsync<DocumentoVehiculoLeido>();
        Assert.True(documento!.TieneArchivo);
        Assert.Equal("poliza.pdf", documento.ArchivoNombre);

        var descarga = await cliente.GetAsync($"/api/flota/documentacion/{documento.Id}/archivo");

        Assert.Equal(HttpStatusCode.OK, descarga.StatusCode);
        Assert.Equal("application/pdf", descarga.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// El archivo se sirve <b>en línea</b>, igual que el del chofer: quien abre un documento lo
    /// quiere ver en el navegador, no bajarlo y abrirlo a mano. La decisión es la misma en los dos
    /// módulos y por eso el resultado sale del mismo helper.
    /// </summary>
    [Fact]
    public async Task El_Archivo_SeSirveEnLinea_ConSuNombre()
    {
        var (vehiculoId, tipoId) = await PrepararAsync("en línea");
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsync(
            $"/api/flota/vehiculos/{vehiculoId}/documentacion",
            AyudasDeDocumentacion.Formulario(
                tipoId,
                diasHastaVencimiento: 300,
                archivo: AyudasDeDocumentacion.Pdf(),
                nombreDeArchivo: "cedula-verde.pdf"));

        var documento = await respuesta.Content.ReadFromJsonAsync<DocumentoVehiculoLeido>();

        var descarga = await cliente.GetAsync($"/api/flota/documentacion/{documento!.Id}/archivo");

        Assert.Equal(HttpStatusCode.OK, descarga.StatusCode);

        var disposicion = descarga.Content.Headers.ContentDisposition;
        Assert.Equal("inline", disposicion!.DispositionType);
        Assert.Contains("cedula-verde.pdf", $"{disposicion.FileName} {disposicion.FileNameStar}");

        Assert.Equal("nosniff", Assert.Single(descarga.Headers.GetValues("X-Content-Type-Options")));
    }

    /// <summary>
    /// US3 esc. 8: un archivo que <b>dice</b> ser PDF por su nombre y no lo es se rechaza. La
    /// validación mira la firma, no la extensión (FR-025).
    /// </summary>
    [Fact]
    public async Task Rechaza_UnArchivoQueDiceSerPdfYNoLoEs()
    {
        var (vehiculoId, tipoId) = await PrepararAsync("con PDF falso");
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsync(
            $"/api/flota/vehiculos/{vehiculoId}/documentacion",
            AyudasDeDocumentacion.Formulario(
                tipoId,
                diasHastaVencimiento: 300,
                archivo: AyudasDeDocumentacion.NoEsUnPdf(),
                nombreDeArchivo: "impostor.pdf"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("archivo_no_admitido", error!.Codigo);
        Assert.Equal(
            "El archivo tiene que ser PDF, JPG o PNG y pesar menos de 10 MB.",
            error.Mensaje);
        Assert.Equal("archivo", error.Campo);
    }

    /// <summary>Un tipo no admitido —texto plano— se rechaza igual (FR-025).</summary>
    [Fact]
    public async Task Rechaza_UnTipoNoAdmitido()
    {
        var (vehiculoId, tipoId) = await PrepararAsync("con texto plano");
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsync(
            $"/api/flota/vehiculos/{vehiculoId}/documentacion",
            AyudasDeDocumentacion.Formulario(
                tipoId,
                diasHastaVencimiento: 300,
                archivo: "esto es texto plano y no un escaneo"u8.ToArray(),
                nombreDeArchivo: "nota.txt"));

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("archivo_no_admitido", error!.Codigo);
    }

    /// <summary>Un archivo que supera los 10 MB se rechaza por tamaño (FR-025).</summary>
    [Fact]
    public async Task Rechaza_UnArchivoQueSuperaElLimite()
    {
        var (vehiculoId, tipoId) = await PrepararAsync("con archivo enorme");
        var cliente = await app.CrearClienteAutenticadoAsync();

        // Un PDF válido por firma pero de más de 10 MB: lo que se prueba es el límite de tamaño.
        var enorme = new byte[10 * 1024 * 1024 + 1024];
        AyudasDeDocumentacion.Pdf().CopyTo(enorme, 0);

        var respuesta = await cliente.PostAsync(
            $"/api/flota/vehiculos/{vehiculoId}/documentacion",
            AyudasDeDocumentacion.Formulario(
                tipoId,
                diasHastaVencimiento: 300,
                archivo: enorme,
                nombreDeArchivo: "enorme.pdf"));

        Assert.NotEqual(HttpStatusCode.Created, respuesta.StatusCode);
    }

    /// <summary>
    /// FR-038 y SC-011: la descarga responde 404 cuando el documento existe pero no tiene archivo.
    /// La ruta interna nunca sale al cliente.
    /// </summary>
    [Fact]
    public async Task La_Descarga_RespondeNoEncontrado_SiElDocumentoNoTieneArchivo()
    {
        var (vehiculoId, tipoId) = await PrepararAsync("sin archivo para descargar");
        var documento = await app.CrearDocumentoVehiculoAsync(vehiculoId, tipoId, 200);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync($"/api/flota/documentacion/{documento.Id}/archivo");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    /// <summary>
    /// FR-027: eliminar un documento se lleva su archivo. Después de eliminarlo, la descarga ya no
    /// encuentra nada.
    /// </summary>
    [Fact]
    public async Task Eliminar_UnDocumento_SeLlevaSuArchivo()
    {
        var (vehiculoId, tipoId) = await PrepararAsync("con archivo que se borra");
        var cliente = await app.CrearClienteAutenticadoAsync();

        var carga = await cliente.PostAsync(
            $"/api/flota/vehiculos/{vehiculoId}/documentacion",
            AyudasDeDocumentacion.Formulario(
                tipoId,
                diasHastaVencimiento: 300,
                archivo: AyudasDeDocumentacion.Pdf()));

        var documento = await carga.Content.ReadFromJsonAsync<DocumentoVehiculoLeido>();

        await cliente.DeleteAsync($"/api/flota/documentacion/{documento!.Id}");

        // Borrado físico: la fila ya no está (FR-028).
        Assert.Null(await app.RecargarDocumentoVehiculoAsync(documento.Id));

        var descarga = await cliente.GetAsync($"/api/flota/documentacion/{documento.Id}/archivo");
        Assert.Equal(HttpStatusCode.NotFound, descarga.StatusCode);
    }

    private async Task<(int VehiculoId, int TipoDocumentacionId)> PrepararAsync(string etiqueta)
    {
        var tipoVehiculo = await app.CrearTipoVehiculoAsync(nombre: $"Tipo {etiqueta}");
        var transportista = await app.CrearTransportistaAsync(nombre: $"Dueño {etiqueta}");
        var vehiculo = await app.CrearVehiculoAsync(tipoVehiculo.Id, transportista.Id);
        var tipoDocumentacion = await app.CrearTipoDocumentacionDeVehiculoAsync(
            nombre: $"Seguro {etiqueta}");

        return (vehiculo.Id, tipoDocumentacion.Id);
    }
}
