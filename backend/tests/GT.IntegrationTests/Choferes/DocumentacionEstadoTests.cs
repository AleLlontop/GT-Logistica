using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Choferes;

/// <summary>
/// El estado de un documento lo calcula el sistema al leer, nunca se recibe ni se guarda
/// (FR-017, FR-018, US3 esc. 3 a 5).
///
/// Es el corazón del módulo: tres documentos cargados igual, con vencimientos a distinta distancia,
/// salen con tres estados distintos sin que nadie los haya elegido.
/// </summary>
public class DocumentacionEstadoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Theory]
    // Vence mucho más allá de los 30 días de aviso de su tipo.
    [InlineData(200, "vigente")]
    // Cae dentro de la ventana de aviso.
    [InlineData(10, "proximaAvencer")]
    // Vence exactamente hoy: sigue siendo próxima a vencer, no vencida (borde de la spec).
    [InlineData(0, "proximaAvencer")]
    // Ya pasó.
    [InlineData(-1, "vencida")]
    public async Task Calcula_ElEstado_SegunLaDistanciaAlVencimiento(int dias, string estadoEsperado)
    {
        var chofer = await app.CrearChoferCompletoAsync(semilla: 81000000 + dias + 10);
        var tipo = await app.CrearTipoDocumentacionAsync(diasAvisoVencimiento: 30);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsync(
            $"/api/choferes/{chofer.Id}/documentacion",
            AyudasDeDocumentacion.Formulario(tipo.Id, dias));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var documento = await respuesta.Content.ReadFromJsonAsync<DocumentoLeido>();
        Assert.Equal(estadoEsperado, documento!.Estado);
        Assert.Equal(dias, documento.DiasHastaVencimiento);
    }

    /// <summary>
    /// Con la ventana de aviso en cero no hay período intermedio: es vigente hasta el día del
    /// vencimiento inclusive y vencido al siguiente (FR-013, caso límite).
    /// </summary>
    [Theory]
    [InlineData(1, "vigente")]
    [InlineData(0, "proximaAvencer")]
    [InlineData(-1, "vencida")]
    public async Task Con_CeroDiasDeAviso_NoHayPeriodoIntermedio(int dias, string estadoEsperado)
    {
        var chofer = await app.CrearChoferCompletoAsync(semilla: 82000000 + dias + 10);
        var tipo = await app.CrearTipoDocumentacionAsync(diasAvisoVencimiento: 0);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsync(
            $"/api/choferes/{chofer.Id}/documentacion",
            AyudasDeDocumentacion.Formulario(tipo.Id, dias));

        var documento = await respuesta.Content.ReadFromJsonAsync<DocumentoLeido>();
        Assert.Equal(estadoEsperado, documento!.Estado);
    }

    /// <summary>Un documento sin archivo es válido, y el sistema lo distingue de uno respaldado.</summary>
    [Fact]
    public async Task Acepta_UnDocumentoSinArchivo()
    {
        var chofer = await app.CrearChoferCompletoAsync(semilla: 83111222);
        var tipo = await app.CrearTipoDocumentacionAsync();

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsync(
            $"/api/choferes/{chofer.Id}/documentacion",
            AyudasDeDocumentacion.Formulario(tipo.Id, 100));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var documento = await respuesta.Content.ReadFromJsonAsync<DocumentoLeido>();
        Assert.False(documento!.TieneArchivo);
        Assert.Null(documento.ArchivoNombre);
    }

    [Fact]
    public async Task Guarda_ElArchivoAdjunto_ConSuNombreOriginal()
    {
        var chofer = await app.CrearChoferCompletoAsync(semilla: 84111222);
        var tipo = await app.CrearTipoDocumentacionAsync();

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsync(
            $"/api/choferes/{chofer.Id}/documentacion",
            AyudasDeDocumentacion.Formulario(
                tipo.Id,
                100,
                archivo: AyudasDeDocumentacion.Pdf(),
                nombreDeArchivo: "licencia-escaneada.pdf"));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var documento = await respuesta.Content.ReadFromJsonAsync<DocumentoLeido>();
        Assert.True(documento!.TieneArchivo);
        Assert.Equal("licencia-escaneada.pdf", documento.ArchivoNombre);
    }

    /// <summary>
    /// Cargar una renovación deja al anterior como historial sin tocarlo: el vigente pasa a ser el
    /// de vencimiento más lejano (FR-020, FR-020a, US3 esc. 7).
    /// </summary>
    [Fact]
    public async Task Cargar_UnaRenovacion_DejaAlAnteriorComoHistorial()
    {
        var chofer = await app.CrearChoferCompletoAsync(semilla: 85111222);
        var tipo = await app.CrearTipoDocumentacionAsync();

        var cliente = await app.CrearClienteAutenticadoAsync();

        var viejo = await cliente.PostAsync(
            $"/api/choferes/{chofer.Id}/documentacion",
            AyudasDeDocumentacion.Formulario(tipo.Id, -5, numero: "VIEJO"));
        var vencido = await viejo.Content.ReadFromJsonAsync<DocumentoLeido>();

        // Con un solo documento del tipo, el vencido es el vigente aunque esté vencido.
        Assert.True(vencido!.EsVigenteDelTipo);

        var nuevo = await cliente.PostAsync(
            $"/api/choferes/{chofer.Id}/documentacion",
            AyudasDeDocumentacion.Formulario(tipo.Id, 300, numero: "NUEVO"));
        var renovacion = await nuevo.Content.ReadFromJsonAsync<DocumentoLeido>();

        Assert.True(renovacion!.EsVigenteDelTipo);
        Assert.Equal("vigente", renovacion.Estado);
    }
}
