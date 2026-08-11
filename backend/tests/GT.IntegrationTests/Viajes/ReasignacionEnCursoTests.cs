using System.Net;
using System.Net.Http.Json;
using GT.Domain.Viajes;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// FR-026a y US3 esc. 16: reasignar un viaje que <b>ya está en curso</b> verifica ocupación.
///
/// <b>El requisito nació de la revisión de calidad de la spec.</b> La exclusividad estaba escrita
/// alrededor del pase a <c>en curso</c>; reasignarle un chofer ocupado a un viaje ya andando dejaba
/// dos viajes con la misma persona, y el índice único lo habría rechazado con un error que ninguna
/// regla explicaba.
///
/// <b>Se implementa en US3 y se prueba acá</b>, porque el estado <c>en curso</c> que el test necesita
/// recién existe en US4 (tasks §Dependencias).
/// </summary>
public class ReasignacionEnCursoTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Reasignar_UnViajeEnCurso_AUnaUnidadOcupada_SeRechaza()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var ocupada = await app.ArmarEscenarioAsync();
        var libre = await app.ArmarEscenarioAsync();

        // El primero toma la unidad y arranca: a partir de acá la ocupa.
        var elQueOcupa = await app.CrearViajeDelEscenarioAsync(ocupada, asignado: true);
        await cliente.PostAsync($"/api/viajes/{elQueOcupa.Id}/en-curso", null);

        // El segundo también está en curso, con su propia unidad.
        var enCurso = await app.CrearViajeDelEscenarioAsync(libre, asignado: true);
        await cliente.PostAsync($"/api/viajes/{enCurso.Id}/en-curso", null);

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/viajes/{enCurso.Id}/asignacion",
            new { choferId = ocupada.ChoferId, vehiculoId = libre.VehiculoId });

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorViajeLeido>();

        Assert.Equal("chofer_ocupado", error!.Codigo);

        var recargado = await app.RecargarViajeAsync(elQueOcupa.Id);
        Assert.Equal(recargado!.Numero, error.ViajeQueOcupa);

        // Y no se guardó nada: el viaje conserva su chofer original.
        var sinCambios = await app.RecargarViajeAsync(enCurso.Id);
        Assert.Equal(libre.ChoferId, sinCambios!.ChoferId);
    }

    /// <summary>
    /// El espejo: reasignar un viaje <b>pendiente</b> a esa misma unidad ocupada <b>se acepta</b>,
    /// porque un pendiente no ocupa a nadie (FR-027). Es lo que permite planificar el viaje de mañana
    /// con el chofer que hoy está en la ruta.
    /// </summary>
    [Fact]
    public async Task Reasignar_UnViajePendiente_AUnaUnidadOcupada_SeAcepta()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var ocupada = await app.ArmarEscenarioAsync();
        var libre = await app.ArmarEscenarioAsync();

        var elQueOcupa = await app.CrearViajeDelEscenarioAsync(ocupada, asignado: true);
        await cliente.PostAsync($"/api/viajes/{elQueOcupa.Id}/en-curso", null);

        var pendiente = await app.CrearViajeDelEscenarioAsync(libre, asignado: true);

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/viajes/{pendiente.Id}/asignacion",
            new { choferId = ocupada.ChoferId, vehiculoId = libre.VehiculoId });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var despues = await app.RecargarViajeAsync(pendiente.Id);

        Assert.Equal(ocupada.ChoferId, despues!.ChoferId);
        Assert.Equal(EstadoViaje.Pendiente, despues.Estado);
    }

    /// <summary>
    /// Reasignar un viaje en curso a una unidad <b>libre</b> procede: la verificación existe para
    /// impedir el solapamiento, no para congelar la asignación.
    /// </summary>
    [Fact]
    public async Task Reasignar_UnViajeEnCurso_AUnaUnidadLibre_Procede()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var original = await app.ArmarEscenarioAsync();
        var reemplazo = await app.ArmarEscenarioAsync();

        var viaje = await app.CrearViajeDelEscenarioAsync(original, asignado: true);
        await cliente.PostAsync($"/api/viajes/{viaje.Id}/en-curso", null);

        var respuesta = await cliente.PostAsJsonAsync(
            $"/api/viajes/{viaje.Id}/asignacion",
            new { choferId = reemplazo.ChoferId, vehiculoId = reemplazo.VehiculoId });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var despues = await app.RecargarViajeAsync(viaje.Id);

        Assert.Equal(reemplazo.ChoferId, despues!.ChoferId);
        Assert.Equal(reemplazo.VehiculoId, despues.VehiculoId);
    }
}
