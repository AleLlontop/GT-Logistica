using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// SC-003 bajo concurrencia: dos altas simultáneas con el mismo remito.
///
/// <b>A mano es imposible de provocar</b> —haría falta que dos personas guarden en el mismo
/// milisegundo— y es exactamente lo que la consulta previa no puede cerrar sola: las dos peticiones
/// la pasan porque en ese instante el remito todavía está libre. Lo que corta a la segunda es el
/// índice único filtrado de la base, y el repositorio traduce esa violación al rechazo que
/// corresponde (research §2, convención [003]).
/// </summary>
public class RemitoConcurrenciaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Dos_AltasSimultaneasConElMismoRemito_UnaGanaYLaOtraRecibeElRechazo()
    {
        var padron = await app.CrearClienteAsync();
        var remito = $"CARRERA-{Guid.NewGuid():N}"[..20];

        // Dos clientes HTTP distintos: dos sesiones, como dos operadores.
        var primero = await app.CrearClienteAutenticadoAsync();
        var segundo = await app.CrearClienteAutenticadoAsync();

        var cuerpo = new
        {
            clienteId = padron.Id,
            fecha = "2026-08-10",
            origen = "Rosario",
            destino = "Córdoba",
            numeroRemito = remito,
        };

        var respuestas = await Task.WhenAll(
            primero.PostAsJsonAsync("/api/viajes", cuerpo),
            segundo.PostAsJsonAsync("/api/viajes", cuerpo));

        var creadas = respuestas.Count(r => r.StatusCode == HttpStatusCode.Created);
        var rechazadas = respuestas.Count(r => r.StatusCode == HttpStatusCode.BadRequest);

        Assert.Equal(1, creadas);
        Assert.Equal(1, rechazadas);

        var rechazo = respuestas.First(r => r.StatusCode == HttpStatusCode.BadRequest);
        var error = await rechazo.Content.ReadFromJsonAsync<ErrorViajeLeido>();

        // El rechazo es el de negocio, no un 500 con el error de la base filtrándose hacia afuera.
        Assert.Equal("remito_duplicado", error!.Codigo);
    }
}
