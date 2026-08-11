using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// FR-019b y US3 esc. 17: <b>no hay asignación parcial</b>.
///
/// El requisito nació de la revisión de calidad de la spec: FR-019 nombraba a los dos juntos, pero
/// US4 esc. 2 suponía un viaje "sin chofer o sin vehículo", estado que sólo se alcanza asignando de a
/// uno. Con los dos obligatorios, un viaje tiene los dos o ninguno, y eso simplifica todo lo que
/// viene después: FR-025 pregunta una sola cosa y FR-022a no tiene que contemplar el caso de una sola
/// unidad asignada.
/// </summary>
public class AsignacionParcialTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Con_SoloElChofer_SeRechaza()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();
        var viaje = await app.CrearViajeDelEscenarioAsync(escenario);

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/viajes/{viaje.Id}/asignacion",
            new { choferId = escenario.ChoferId });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();
        Assert.Equal("vehiculo_inexistente", error!.Codigo);
        Assert.Equal("vehiculoId", error.Campo);

        await NoQuedoNadaAsignado(viaje.Id);
    }

    [Fact]
    public async Task Con_SoloElVehiculo_SeRechaza()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();
        var viaje = await app.CrearViajeDelEscenarioAsync(escenario);

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/viajes/{viaje.Id}/asignacion",
            new { vehiculoId = escenario.VehiculoId });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();
        Assert.Equal("chofer_inexistente", error!.Codigo);
        Assert.Equal("choferId", error.Campo);

        await NoQuedoNadaAsignado(viaje.Id);
    }

    [Fact]
    public async Task Con_LosDosEnNulo_SeRechaza()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();
        var viaje = await app.CrearViajeDelEscenarioAsync(escenario);

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/viajes/{viaje.Id}/asignacion",
            new { choferId = (int?)null, vehiculoId = (int?)null });

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        await NoQuedoNadaAsignado(viaje.Id);
    }

    private async Task NoQuedoNadaAsignado(int viajeId)
    {
        var viaje = await app.RecargarViajeAsync(viajeId);

        Assert.Null(viaje!.ChoferId);
        Assert.Null(viaje.VehiculoId);
    }
}
