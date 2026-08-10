using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// Baja y reactivación de una unidad (FR-008, FR-008e, FR-031, FR-035, US6 esc. 5, 8 y 9).
///
/// La baja es lógica y <b>no toca la documentación</b>: los documentos y sus archivos se conservan
/// intactos, y por eso la unidad vuelve completa al reactivarla (FR-028).
/// </summary>
public class BajaReactivacionVehiculoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task La_Baja_ConservaDocumentosYArchivos_YSacaLaUnidadDelListadoYDelPanel()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo de la baja lógica");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño de la baja lógica");
        var tipoDocumento = await app.CrearTipoDocumentacionDeVehiculoAsync(
            nombre: "Seguro de la baja lógica");

        var vehiculo = await app.CrearVehiculoAsync(tipo.Id, transportista.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        // Un documento con archivo, y vencido, para que además esté alertando antes de la baja.
        var carga = await cliente.PostAsync(
            $"/api/flota/vehiculos/{vehiculo.Id}/documentacion",
            AyudasDeDocumentacion.Formulario(
                tipoDocumento.Id,
                diasHastaVencimiento: -10,
                archivo: AyudasDeDocumentacion.Pdf()));

        var documento = await carga.Content.ReadFromJsonAsync<DocumentoVehiculoLeido>();

        var antesDelPanel = await cliente.GetFromJsonAsync<List<AlertaFlotaLeida>>(
            "/api/flota/vencimientos");
        Assert.Contains(antesDelPanel!, a => a.VehiculoId == vehiculo.Id);

        var baja = await cliente.DeleteAsync($"/api/flota/vehiculos/{vehiculo.Id}");
        Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);

        // Sale del listado por defecto (FR-031)…
        var listado = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(
            $"/api/flota/vehiculos?transportistaId={transportista.Id}");
        Assert.DoesNotContain(listado!.Items, v => v.Id == vehiculo.Id);

        // …y del panel (FR-035).
        var panel = await cliente.GetFromJsonAsync<List<AlertaFlotaLeida>>("/api/flota/vencimientos");
        Assert.DoesNotContain(panel!, a => a.VehiculoId == vehiculo.Id);

        // Pero su ficha y su documentación siguen enteras (FR-008).
        var ficha = await cliente.GetFromJsonAsync<VehiculoDetalleLeido>(
            $"/api/flota/vehiculos/{vehiculo.Id}");

        Assert.False(ficha!.Activo);
        Assert.Single(ficha.Documentos);
        Assert.True(Assert.Single(ficha.Documentos).TieneArchivo);

        // Y el archivo se sigue pudiendo descargar: la baja no borra nada.
        var descarga = await cliente.GetAsync($"/api/flota/documentacion/{documento!.Id}/archivo");
        Assert.Equal(HttpStatusCode.OK, descarga.StatusCode);
    }

    /// <summary>US6 esc. 8: aparece con el filtro <c>dadoDeBaja</c> (FR-030a).</summary>
    [Fact]
    public async Task La_UnidadDadaDeBaja_ApareceConSuFiltro()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del filtro de baja");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del filtro de baja");
        var vehiculo = await app.CrearVehiculoAsync(tipo.Id, transportista.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();
        await cliente.DeleteAsync($"/api/flota/vehiculos/{vehiculo.Id}");

        var dadosDeBaja = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(
            $"/api/flota/vehiculos?transportistaId={transportista.Id}&estado=dadoDeBaja");

        Assert.Contains(dadosDeBaja!.Items, v => v.Id == vehiculo.Id);
    }

    /// <summary>
    /// US6 esc. 9: al reactivarla vuelve al listado y al panel <b>con toda su documentación</b>
    /// contando de nuevo, sin recargar nada (FR-008e).
    /// </summary>
    [Fact]
    public async Task La_Reactivacion_DevuelveLaUnidadConSuDocumentacion()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo de la vuelta");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño de la vuelta");
        var tipoDocumento = await app.CrearTipoDocumentacionDeVehiculoAsync(nombre: "Seguro de la vuelta");

        var vehiculo = await app.CrearVehiculoAsync(tipo.Id, transportista.Id, activo: false);
        await app.CrearDocumentoVehiculoAsync(vehiculo.Id, tipoDocumento.Id, -15);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var reactivacion = await cliente.PostAsJsonAsync(
            $"/api/flota/vehiculos/{vehiculo.Id}/reactivacion",
            new { });

        Assert.Equal(HttpStatusCode.NoContent, reactivacion.StatusCode);

        var listado = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(
            $"/api/flota/vehiculos?transportistaId={transportista.Id}");
        Assert.Contains(listado!.Items, v => v.Id == vehiculo.Id);

        var panel = await cliente.GetFromJsonAsync<List<AlertaFlotaLeida>>("/api/flota/vencimientos");
        Assert.Contains(panel!, a => a.VehiculoId == vehiculo.Id);

        var ficha = await cliente.GetFromJsonAsync<VehiculoDetalleLeido>(
            $"/api/flota/vehiculos/{vehiculo.Id}");
        Assert.True(ficha!.Activo);
        Assert.Single(ficha.Documentos);
    }

    /// <summary>
    /// Dar de baja dos veces no es un error: el resultado buscado ya se cumple, y fallar sólo
    /// complicaría a quien tocó dos veces el botón.
    /// </summary>
    [Fact]
    public async Task Dar_DeBajaDosVeces_NoEsUnError()
    {
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo de la doble baja");
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño de la doble baja");
        var vehiculo = await app.CrearVehiculoAsync(tipo.Id, transportista.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        await cliente.DeleteAsync($"/api/flota/vehiculos/{vehiculo.Id}");
        var segunda = await cliente.DeleteAsync($"/api/flota/vehiculos/{vehiculo.Id}");

        Assert.Equal(HttpStatusCode.NoContent, segunda.StatusCode);
    }

    [Fact]
    public async Task Responde_NoEncontrado_AlDarDeBajaUnaUnidadInexistente()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.DeleteAsync("/api/flota/vehiculos/999999");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
