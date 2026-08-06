using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Usuarios;

namespace GT.IntegrationTests.Choferes;

/// <summary>
/// Baja lógica y reactivación de un chofer (FR-005, FR-005a, FR-005b).
///
/// La baja no borra: lo saca del listado por defecto y del panel de vencimientos, y le conserva la
/// documentación intacta para que vuelva completa si se lo reactiva.
/// </summary>
public class BajaChoferTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Un_ChoferDadoDeBaja_SaleDelListadoPorDefecto_YDelPanel()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Bajas de chofer");
        var tipo = await app.CrearTipoDocumentacionAsync(nombre: "Licencia de baja");

        var chofer = await app.CrearChoferCompletoAsync(20111222, transportistaId: transportista.Id);
        await app.CrearDocumentoAsync(chofer.Id, tipo.Id, -30);

        var cliente = await app.CrearClienteAutenticadoAsync();

        // Antes de la baja está en las dos vistas.
        var listadoAntes = await cliente.GetFromJsonAsync<PaginaLeida>(
            $"/api/choferes?transportistaId={transportista.Id}");
        Assert.Contains(listadoAntes!.Items, fila => fila.Id == chofer.Id);

        var panelAntes = await cliente.GetFromJsonAsync<List<VencimientosTests.AlertaLeida>>(
            "/api/vencimientos");
        Assert.Contains(panelAntes!, alerta => alerta.ChoferId == chofer.Id);

        var baja = await cliente.DeleteAsync($"/api/choferes/{chofer.Id}");
        Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);

        // Después no está en ninguna de las dos.
        var listadoDespues = await cliente.GetFromJsonAsync<PaginaLeida>(
            $"/api/choferes?transportistaId={transportista.Id}");
        Assert.DoesNotContain(listadoDespues!.Items, fila => fila.Id == chofer.Id);

        var panelDespues = await cliente.GetFromJsonAsync<List<VencimientosTests.AlertaLeida>>(
            "/api/vencimientos");
        Assert.DoesNotContain(panelDespues!, alerta => alerta.ChoferId == chofer.Id);

        // Pero aparece filtrando por inactivo, y su documentación sigue entera (FR-005a).
        var inactivos = await cliente.GetFromJsonAsync<PaginaLeida>(
            $"/api/choferes?transportistaId={transportista.Id}&estado=inactivo");
        Assert.Contains(inactivos!.Items, fila => fila.Id == chofer.Id);

        var ficha = await cliente.GetFromJsonAsync<ChoferConDocumentos>($"/api/choferes/{chofer.Id}");
        Assert.False(ficha!.Activo);
        Assert.Single(ficha.Documentos);
    }

    [Fact]
    public async Task Reactivar_LoDevuelveAlListado_ConSuDocumentacion()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Reactivaciones");
        var tipo = await app.CrearTipoDocumentacionAsync(nombre: "Licencia reactivada");

        var chofer = await app.CrearChoferCompletoAsync(
            20211222,
            activo: false,
            transportistaId: transportista.Id);
        await app.CrearDocumentoAsync(chofer.Id, tipo.Id, -30);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsync($"/api/choferes/{chofer.Id}/reactivacion", null);
        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);

        var listado = await cliente.GetFromJsonAsync<PaginaLeida>(
            $"/api/choferes?transportistaId={transportista.Id}");
        Assert.Contains(listado!.Items, fila => fila.Id == chofer.Id);

        // Y vuelve a alertar sin que nadie recargue nada: el estado se calcula al consultarlo.
        var panel = await cliente.GetFromJsonAsync<List<VencimientosTests.AlertaLeida>>(
            "/api/vencimientos");
        Assert.Contains(panel!, alerta => alerta.ChoferId == chofer.Id);
    }

    [Fact]
    public async Task Rechaza_ReactivarUnChoferQueYaEstaActivo()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Ya activo");
        var chofer = await app.CrearChoferCompletoAsync(20311222, transportistaId: transportista.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsync($"/api/choferes/{chofer.Id}/reactivacion", null);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorLeido>();
        Assert.Equal("datos_invalidos", error!.Codigo);
    }

    /// <summary>
    /// Reactivar con el transportista dado de baja dejaría un chofer activo colgando de uno
    /// inactivo, que es lo que FR-008 no admite (FR-005b).
    /// </summary>
    [Fact]
    public async Task Rechaza_ReactivarSiSuTransportistaQuedoInactivo()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Transportista de baja");
        var chofer = await app.CrearChoferCompletoAsync(
            20411222,
            activo: false,
            transportistaId: transportista.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        // Sin choferes activos, el transportista se puede dar de baja.
        var baja = await cliente.DeleteAsync($"/api/transportistas/{transportista.Id}");
        Assert.Equal(HttpStatusCode.NoContent, baja.StatusCode);

        var respuesta = await cliente.PostAsync($"/api/choferes/{chofer.Id}/reactivacion", null);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorLeido>();
        Assert.Equal("transportista_inexistente", error!.Codigo);
    }

    [Fact]
    public async Task Responde_NoEncontrado_AlDarDeBajaUnChoferInexistente()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.DeleteAsync("/api/choferes/999999");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
