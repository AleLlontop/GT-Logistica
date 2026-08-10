using System.Net.Http.Json;
using GT.Domain.Flota;
using GT.IntegrationTests.Choferes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Flota;

/// <summary>
/// Los cuatro filtros del listado, combinables entre sí (FR-030, FR-030a, FR-031, US4 esc. 2 y 3).
///
/// El que más importa: <b>sin filtro de estado se devuelven sólo los activos</b>, que no es lo mismo
/// que "todos". Los dados de baja se piden explícitamente (FR-031).
/// </summary>
public class FiltrosFlotaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Sin_FiltroDeEstado_DevuelveSoloLosActivos()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del filtro por defecto");
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del filtro por defecto");

        var activo = await app.CrearVehiculoAsync(tipo.Id, transportista.Id);
        var dadoDeBaja = await app.CrearVehiculoAsync(tipo.Id, transportista.Id, activo: false);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var pagina = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(
            $"/api/flota/vehiculos?transportistaId={transportista.Id}");

        Assert.Contains(pagina!.Items, v => v.Id == activo.Id);
        Assert.DoesNotContain(pagina.Items, v => v.Id == dadoDeBaja.Id);
    }

    /// <summary>US4 esc. 3: los dados de baja aparecen eligiendo <c>dadoDeBaja</c> (FR-030a).</summary>
    [Fact]
    public async Task El_FiltroDadoDeBaja_DevuelveSoloLosInactivos()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del filtro dadoDeBaja");
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del filtro dadoDeBaja");

        var activo = await app.CrearVehiculoAsync(tipo.Id, transportista.Id);
        var dadoDeBaja = await app.CrearVehiculoAsync(tipo.Id, transportista.Id, activo: false);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var pagina = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(
            $"/api/flota/vehiculos?transportistaId={transportista.Id}&estado=dadoDeBaja");

        Assert.Contains(pagina!.Items, v => v.Id == dadoDeBaja.Id);
        Assert.DoesNotContain(pagina.Items, v => v.Id == activo.Id);
    }

    /// <summary>US4 esc. 2: el filtro por transportista responde quién es dueño de qué (SC-003b).</summary>
    [Fact]
    public async Task Filtra_PorTransportista()
    {
        var propio = await app.CrearTransportistaAsync(nombre: "Flota propia");
        var contratado = await app.CrearTransportistaAsync(nombre: "Flota contratada");
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo compartido entre transportistas");

        var delPropio = await app.CrearVehiculoAsync(tipo.Id, propio.Id);
        var delContratado = await app.CrearVehiculoAsync(tipo.Id, contratado.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var pagina = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(
            $"/api/flota/vehiculos?transportistaId={propio.Id}");

        Assert.Contains(pagina!.Items, v => v.Id == delPropio.Id);
        Assert.DoesNotContain(pagina.Items, v => v.Id == delContratado.Id);
    }

    [Fact]
    public async Task Filtra_PorTipoDeVehiculo()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del filtro por tipo");
        var tractor = await app.CrearTipoVehiculoAsync(nombre: "Tractor del filtro");
        var semirremolque = await app.CrearTipoVehiculoAsync(nombre: "Semirremolque del filtro");

        var unTractor = await app.CrearVehiculoAsync(tractor.Id, transportista.Id);
        var unSemi = await app.CrearVehiculoAsync(semirremolque.Id, transportista.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var pagina = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(
            $"/api/flota/vehiculos?transportistaId={transportista.Id}&tipoVehiculoId={tractor.Id}");

        Assert.Contains(pagina!.Items, v => v.Id == unTractor.Id);
        Assert.DoesNotContain(pagina.Items, v => v.Id == unSemi.Id);
    }

    /// <summary>Los cuatro valores del estado de documentación, sobre la misma flota (FR-033).</summary>
    [Fact]
    public async Task Filtra_PorEstadoDeDocumentacion()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del filtro documental");
        var tipoVehiculo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del filtro documental");
        var tipoDocumento = await app.CrearTipoDocumentacionDeVehiculoAsync(
            nombre: "Seguro del filtro documental",
            diasAvisoVencimiento: 30);

        var sinPapeles = await app.CrearVehiculoAsync(tipoVehiculo.Id, transportista.Id);

        var enRegla = await app.CrearVehiculoAsync(tipoVehiculo.Id, transportista.Id);
        await app.CrearDocumentoVehiculoAsync(enRegla.Id, tipoDocumento.Id, 300);

        var porVencer = await app.CrearVehiculoAsync(tipoVehiculo.Id, transportista.Id);
        await app.CrearDocumentoVehiculoAsync(porVencer.Id, tipoDocumento.Id, 10);

        var vencido = await app.CrearVehiculoAsync(tipoVehiculo.Id, transportista.Id);
        await app.CrearDocumentoVehiculoAsync(vencido.Id, tipoDocumento.Id, -5);

        var cliente = await app.CrearClienteAutenticadoAsync();

        await AssertUnicoAsync(cliente, transportista.Id, "sinDocumentacion", sinPapeles.Id);
        await AssertUnicoAsync(cliente, transportista.Id, "enRegla", enRegla.Id);
        await AssertUnicoAsync(cliente, transportista.Id, "proximaAvencer", porVencer.Id);
        await AssertUnicoAsync(cliente, transportista.Id, "vencida", vencido.Id);
    }

    /// <summary>Los cuatro filtros se combinan con "y" (FR-030).</summary>
    [Fact]
    public async Task Combina_LosCuatroFiltros()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño de la combinación");
        var tipoVehiculo = await app.CrearTipoVehiculoAsync(nombre: "Tipo de la combinación");
        var otroTipo = await app.CrearTipoVehiculoAsync(nombre: "Otro tipo de la combinación");
        var tipoDocumento = await app.CrearTipoDocumentacionDeVehiculoAsync(
            nombre: "Seguro de la combinación");

        // El que cumple los cuatro: activo, disponible, de ese transportista y de ese tipo.
        var buscado = await app.CrearVehiculoAsync(
            tipoVehiculo.Id,
            transportista.Id,
            estadoOperativo: VehiculoEstado.Disponible);
        await app.CrearDocumentoVehiculoAsync(buscado.Id, tipoDocumento.Id, 300);

        // Mismo estado pero de otro tipo.
        var deOtroTipo = await app.CrearVehiculoAsync(
            otroTipo.Id,
            transportista.Id,
            estadoOperativo: VehiculoEstado.Disponible);
        await app.CrearDocumentoVehiculoAsync(deOtroTipo.Id, tipoDocumento.Id, 300);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var pagina = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(
            $"/api/flota/vehiculos?transportistaId={transportista.Id}" +
            $"&tipoVehiculoId={tipoVehiculo.Id}&estado=disponible&estadoDocumentacion=enRegla");

        var fila = Assert.Single(pagina!.Items);
        Assert.Equal(buscado.Id, fila.Id);
        Assert.DoesNotContain(pagina.Items, v => v.Id == deOtroTipo.Id);
    }

    /// <summary>Un valor de filtro desconocido no rompe: se ignora y vuelve al listado por defecto.</summary>
    [Fact]
    public async Task Un_ValorDeFiltroDesconocido_SeIgnora()
    {
        var transportista = await app.CrearTransportistaAsync(nombre: "Dueño del filtro raro");
        var tipo = await app.CrearTipoVehiculoAsync(nombre: "Tipo del filtro raro");
        var vehiculo = await app.CrearVehiculoAsync(tipo.Id, transportista.Id);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var pagina = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(
            $"/api/flota/vehiculos?transportistaId={transportista.Id}&estado=enOrbita");

        Assert.Contains(pagina!.Items, v => v.Id == vehiculo.Id);
    }

    private static async Task AssertUnicoAsync(
        HttpClient cliente,
        int transportistaId,
        string estadoDocumentacion,
        int vehiculoEsperado)
    {
        var pagina = await cliente.GetFromJsonAsync<PaginaDeVehiculos>(
            $"/api/flota/vehiculos?transportistaId={transportistaId}" +
            $"&estadoDocumentacion={estadoDocumentacion}");

        var fila = Assert.Single(pagina!.Items);
        Assert.Equal(vehiculoEsperado, fila.Id);
        Assert.Equal(estadoDocumentacion, fila.EstadoDocumentacion);
    }
}
