using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// SC-005 bajo concurrencia: dos operadores ponen en curso el mismo chofer al mismo tiempo.
///
/// <b>A mano es imposible de provocar</b> y es lo que sostiene el 0% de SC-005. La consulta previa
/// deja pasar a las dos peticiones porque en ese instante la unidad todavía está libre; lo que corta
/// a la segunda es el índice único filtrado <c>IX_Viajes_ChoferEnCurso</c>, y el repositorio traduce
/// esa violación al rechazo de negocio (research §2).
///
/// Es el motivo por el que la exclusividad se resolvió con un índice y no sólo con una consulta.
/// </summary>
public class ExclusividadConcurrenciaTests(AplicacionDePrueba app)
    : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Dos_OperadoresPoniendoEnCursoElMismoChofer_UnoGanaYElOtroRecibeElRechazo()
    {
        var escenario = await app.ArmarEscenarioAsync();
        var otro = await app.ArmarEscenarioAsync();

        // Dos viajes distintos con el mismo chofer y vehículos distintos, los dos pendientes: un
        // pendiente no ocupa, así que hasta acá conviven sin problema (FR-027).
        var primero = await app.CrearViajeDelEscenarioAsync(escenario, asignado: true);

        var segundo = await app.CrearViajeAsync(
            escenario.ClienteId,
            choferId: escenario.ChoferId,
            vehiculoId: otro.VehiculoId,
            transportistaId: escenario.TransportistaId);

        var operadorA = await app.CrearClienteAutenticadoAsync();
        var operadorB = await app.CrearClienteAutenticadoAsync();

        var respuestas = await Task.WhenAll(
            operadorA.PostAsync($"/api/viajes/{primero.Id}/en-curso", null),
            operadorB.PostAsync($"/api/viajes/{segundo.Id}/en-curso", null));

        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.Equal(1, respuestas.Count(r => r.StatusCode == HttpStatusCode.Conflict));

        var rechazo = respuestas.First(r => r.StatusCode == HttpStatusCode.Conflict);
        var error = await rechazo.Content.ReadFromJsonAsync<ErrorViajeLeido>();

        // El rechazo es el de negocio, no un 500 con el error de la base filtrándose hacia afuera.
        Assert.Equal("chofer_ocupado", error!.Codigo);
    }
}
