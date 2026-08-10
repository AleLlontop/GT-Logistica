using System.Net;
using System.Net.Http.Json;
using GT.Application.Choferes.Documentacion;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Infraestructura;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// FR-029, <b>el único requisito del módulo sin escenario de aceptación</b>: describe una falla de
/// almacenamiento que nadie puede provocar desde la pantalla. Se verifica sustituyendo el almacén por
/// uno que siempre falla (plan §Constitution Check, Principio IV).
///
/// Lo que se garantiza es que la operación sea <b>todo o nada</b>: nunca puede quedar una fila que
/// dice tener adjunto y no lo tiene. El único estado roto posible es un archivo huérfano, invisible
/// para quien opera (convención [003]).
///
/// Suma el camino exitoso de FR-026a: al reemplazar bien un adjunto, el anterior <b>se borra</b>
/// (CHK023).
/// </summary>
public class AtomicidadAdjuntoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    /// <summary>Al cargar con el almacén caído, el documento <b>no</b> queda creado.</summary>
    [Fact]
    public async Task Si_ElArchivoNoSeGuarda_ElDocumentoNoSeCrea()
    {
        var (vehiculoId, tipoId) = await PrepararAsync("de la carga que falla");

        using var conAlmacenRoto = app.ConAlmacenQueFalla();
        var cliente = await ClienteAutenticadoAsync(conAlmacenRoto);

        var respuesta = await cliente.PostAsync(
            $"/api/flota/vehiculos/{vehiculoId}/documentacion",
            AyudasDeDocumentacion.Formulario(
                tipoId,
                diasHastaVencimiento: 300,
                archivo: AyudasDeDocumentacion.Pdf()));

        Assert.Equal(HttpStatusCode.InternalServerError, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorFlotaLeido>();
        Assert.Equal("archivo_no_guardado", error!.Codigo);
        Assert.Equal(
            "No se pudo guardar el archivo. El documento no se modificó; volvé a intentar.",
            error.Mensaje);

        // Lo que FR-029 prohíbe: una fila que dice tener adjunto sin tenerlo. Acá no hay ninguna fila.
        var normal = await app.CrearClienteAutenticadoAsync();
        var ficha = await normal.GetFromJsonAsync<VehiculoDetalleLeido>(
            $"/api/flota/vehiculos/{vehiculoId}");

        Assert.Empty(ficha!.Documentos);
    }

    /// <summary>
    /// Al corregir con un archivo de reemplazo que falla, el documento <b>no queda modificado ni
    /// pierde el adjunto anterior</b>: tiene que quedar exactamente como estaba (FR-029).
    /// </summary>
    [Fact]
    public async Task Si_ElReemplazoFalla_ElDocumentoQuedaComoEstaba()
    {
        var (vehiculoId, tipoId) = await PrepararAsync("de la corrección que falla");
        var cliente = await app.CrearClienteAutenticadoAsync();

        var carga = await cliente.PostAsync(
            $"/api/flota/vehiculos/{vehiculoId}/documentacion",
            AyudasDeDocumentacion.Formulario(
                tipoId,
                diasHastaVencimiento: 300,
                archivo: AyudasDeDocumentacion.Pdf("original"),
                nombreDeArchivo: "original.pdf",
                numero: "POL-ORIGINAL"));

        var documento = await carga.Content.ReadFromJsonAsync<DocumentoVehiculoLeido>();
        var antes = await app.RecargarDocumentoVehiculoAsync(documento!.Id);

        using var conAlmacenRoto = app.ConAlmacenQueFalla();
        var clienteRoto = await ClienteAutenticadoAsync(conAlmacenRoto);

        var correccion = await clienteRoto.PutAsync(
            $"/api/flota/documentacion/{documento.Id}",
            AyudasDeDocumentacion.Formulario(
                tipoId,
                diasHastaVencimiento: 999,
                archivo: AyudasDeDocumentacion.Pdf("reemplazo"),
                nombreDeArchivo: "reemplazo.pdf",
                numero: "POL-CORREGIDA"));

        Assert.Equal(HttpStatusCode.InternalServerError, correccion.StatusCode);

        var despues = await app.RecargarDocumentoVehiculoAsync(documento.Id);

        // Ni el número, ni las fechas, ni el adjunto: nada se movió.
        Assert.Equal(antes!.Numero, despues!.Numero);
        Assert.Equal(antes.FechaVencimiento, despues.FechaVencimiento);
        Assert.Equal(antes.ArchivoRuta, despues.ArchivoRuta);
        Assert.Equal(antes.ArchivoNombre, despues.ArchivoNombre);

        // Y el adjunto original sigue siendo descargable.
        var descarga = await cliente.GetAsync($"/api/flota/documentacion/{documento.Id}/archivo");
        Assert.Equal(HttpStatusCode.OK, descarga.StatusCode);
    }

    /// <summary>
    /// FR-026a y CHK023, el camino exitoso: al reemplazar el adjunto, el documento apunta al nuevo y
    /// <b>el archivo anterior queda borrado</b>. Un escaneo que ya no corresponde deja de existir en
    /// vez de quedar guardado por las dudas.
    /// </summary>
    [Fact]
    public async Task Al_ReemplazarElAdjunto_ElArchivoAnteriorSeBorra()
    {
        var (vehiculoId, tipoId) = await PrepararAsync("del reemplazo exitoso");
        var cliente = await app.CrearClienteAutenticadoAsync();

        var carga = await cliente.PostAsync(
            $"/api/flota/vehiculos/{vehiculoId}/documentacion",
            AyudasDeDocumentacion.Formulario(
                tipoId,
                diasHastaVencimiento: 300,
                archivo: AyudasDeDocumentacion.Pdf("viejo"),
                nombreDeArchivo: "viejo.pdf"));

        var documento = await carga.Content.ReadFromJsonAsync<DocumentoVehiculoLeido>();
        var antes = await app.RecargarDocumentoVehiculoAsync(documento!.Id);
        var rutaAnterior = antes!.ArchivoRuta!;

        var correccion = await cliente.PutAsync(
            $"/api/flota/documentacion/{documento.Id}",
            AyudasDeDocumentacion.Formulario(
                tipoId,
                diasHastaVencimiento: 300,
                archivo: AyudasDeDocumentacion.Pdf("nuevo"),
                nombreDeArchivo: "nuevo.pdf"));

        Assert.Equal(HttpStatusCode.OK, correccion.StatusCode);

        var despues = await app.RecargarDocumentoVehiculoAsync(documento.Id);

        // El documento apunta al nuevo…
        Assert.NotEqual(rutaAnterior, despues!.ArchivoRuta);
        Assert.Equal("nuevo.pdf", despues.ArchivoNombre);

        // …y el anterior ya no está en el volumen (CHK023).
        var almacen = app.Services.GetRequiredService<IAlmacenDeArchivos>();
        Assert.Null(await almacen.AbrirAsync(rutaAnterior));
        Assert.NotNull(await almacen.AbrirAsync(despues.ArchivoRuta!));
    }

    /// <summary>
    /// Y sin archivo nuevo, el que ya tenía <b>se conserva</b>: corregir sólo las fechas no puede
    /// dejar al documento sin respaldo (FR-026).
    /// </summary>
    [Fact]
    public async Task Al_CorregirSinArchivo_ElAdjuntoActualSeConserva()
    {
        var (vehiculoId, tipoId) = await PrepararAsync("de la corrección sin archivo");
        var cliente = await app.CrearClienteAutenticadoAsync();

        var carga = await cliente.PostAsync(
            $"/api/flota/vehiculos/{vehiculoId}/documentacion",
            AyudasDeDocumentacion.Formulario(
                tipoId,
                diasHastaVencimiento: 300,
                archivo: AyudasDeDocumentacion.Pdf(),
                nombreDeArchivo: "conservado.pdf"));

        var documento = await carga.Content.ReadFromJsonAsync<DocumentoVehiculoLeido>();
        var antes = await app.RecargarDocumentoVehiculoAsync(documento!.Id);

        var correccion = await cliente.PutAsync(
            $"/api/flota/documentacion/{documento.Id}",
            AyudasDeDocumentacion.Formulario(tipoId, diasHastaVencimiento: 500));

        Assert.Equal(HttpStatusCode.OK, correccion.StatusCode);

        var despues = await app.RecargarDocumentoVehiculoAsync(documento.Id);

        Assert.Equal(antes!.ArchivoRuta, despues!.ArchivoRuta);
        Assert.Equal("conservado.pdf", despues.ArchivoNombre);
    }

    private static async Task<HttpClient> ClienteAutenticadoAsync(
        Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> fabrica)
    {
        var cliente = fabrica.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri("https://localhost"),
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                username = GT.Infrastructure.DatosIniciales.SembradorInicial.UsernameAdministrador,
                password = AplicacionDePrueba.PasswordAdministrador,
            });

        respuesta.EnsureSuccessStatusCode();

        return cliente;
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
