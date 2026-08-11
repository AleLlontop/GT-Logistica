using System.Net;
using System.Net.Http.Json;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// Las transiciones permitidas, contra los endpoints reales (FR-033, US4 esc. 10).
///
/// <b>Cada transición es un recurso propio</b>, no un campo del <c>PUT</c>: eso es lo que hace que
/// corregir un destino no pueda avanzar un viaje (FR-034).
/// </summary>
public class TransicionesTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task El_CicloCompleto_Funciona()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();
        var viaje = await app.CrearViajeDelEscenarioAsync(escenario, asignado: true, importe: 240_000m);

        var enCurso = await cliente.PostAsync($"/api/viajes/{viaje.Id}/en-curso", null);
        Assert.Equal(HttpStatusCode.OK, enCurso.StatusCode);

        var rendicion = await cliente.PostAsync($"/api/viajes/{viaje.Id}/rendicion", null);
        Assert.Equal(HttpStatusCode.OK, rendicion.StatusCode);

        var final = await app.RecargarViajeAsync(viaje.Id);
        Assert.Equal(EstadoViaje.Rendido, final!.Estado);
    }

    /// <summary>
    /// US4 esc. 10: la pantalla no ofrece *Rendir* en un viaje pendiente, pero el endpoint lo rechaza
    /// igual si se lo invoca a mano. La regla no vive sólo en la pantalla.
    /// </summary>
    [Fact]
    public async Task Pendiente_A_Rendido_SeRechazaAunqueSeInvoqueElEndpoint()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var padron = await app.CrearClienteAsync();
        var viaje = await app.CrearViajeAsync(padron.Id, importe: 100_000m);

        var respuesta = await cliente.PostAsync($"/api/viajes/{viaje.Id}/rendicion", null);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();

        Assert.Equal("transicion_no_permitida", error!.Codigo);
        Assert.Contains("de pendiente a rendido", error.Mensaje);

        var despues = await app.RecargarViajeAsync(viaje.Id);
        Assert.Equal(EstadoViaje.Pendiente, despues!.Estado);
    }

    /// <summary>No hay camino de vuelta: un viaje en curso no se puede volver a poner en curso.</summary>
    [Fact]
    public async Task EnCurso_A_EnCurso_SeRechaza()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var escenario = await app.ArmarEscenarioAsync();

        var viaje = await app.CrearViajeDelEscenarioAsync(
            escenario,
            estado: EstadoViaje.EnCurso,
            asignado: true);

        var respuesta = await cliente.PostAsync($"/api/viajes/{viaje.Id}/en-curso", null);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();
        Assert.Equal("transicion_no_permitida", error!.Codigo);
    }
}
