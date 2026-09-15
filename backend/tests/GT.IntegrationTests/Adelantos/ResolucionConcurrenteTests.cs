using System.Net;
using System.Net.Http.Json;
using GT.Domain.Adelantos;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Adelantos;

/// <summary>
/// Dos resoluciones que se cruzan sobre el mismo pendiente (FR-026, research §2): las dos empiezan con un
/// <c>UPDATE</c> condicional sobre la misma fila, así que exactamente una gana y la otra dice el estado en
/// que quedó. Es lo que el recorrido manual no puede provocar (research §11).
/// </summary>
public class ResolucionConcurrenteTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Una_AprobacionYUnRechazoALaVez_DejanExactamenteUnaResolucion()
    {
        for (var vuelta = 0; vuelta < 3; vuelta++)
        {
            var persona = await app.EmpleadoAsync();
            var adelanto = await app.CrearAdelantoAsync(persona.Id);

            var aprobador = await app.CrearClienteAutenticadoAsync();
            var rechazador = await app.CrearClienteAutenticadoAsync();

            var respuestas = await Task.WhenAll(
                aprobador.AprobarAsync(adelanto.Id),
                rechazador.RechazarAsync(adelanto.Id, "Ya tiene otro pendiente.", confirmado: true));

            await AfirmarUnaSolaResolucionAsync(adelanto.Id, respuestas);
        }
    }

    [Fact]
    public async Task Dos_AprobacionesALaVez_DejanUnaSolaAprobacion()
    {
        for (var vuelta = 0; vuelta < 3; vuelta++)
        {
            var persona = await app.EmpleadoAsync();
            var adelanto = await app.CrearAdelantoAsync(persona.Id);

            var primero = await app.CrearClienteAutenticadoAsync();
            var segundo = await app.CrearClienteAutenticadoAsync();

            var respuestas = await Task.WhenAll(primero.AprobarAsync(adelanto.Id), segundo.AprobarAsync(adelanto.Id));

            await AfirmarUnaSolaResolucionAsync(adelanto.Id, respuestas);
        }
    }

    private async Task AfirmarUnaSolaResolucionAsync(int id, HttpResponseMessage[] respuestas)
    {
        Assert.Equal(1, respuestas.Count(respuesta => respuesta.StatusCode == HttpStatusCode.OK));

        var perdedora = Assert.Single(respuestas, respuesta => respuesta.StatusCode != HttpStatusCode.OK);
        Assert.Equal(HttpStatusCode.Conflict, perdedora.StatusCode);

        var releido = (await app.RecargarAdelantoAsync(id))!;
        var error = (await perdedora.Content.ReadFromJsonAsync<ErrorAdelantoLeido>())!;

        Assert.Equal("adelanto_no_resoluble", error.Codigo);
        Assert.Equal(releido.Estado.ToString().ToLowerInvariant(), error.Estado);
        Assert.Single(
            releido.Cambios,
            cambio => cambio.Operacion is OperacionDeAdelanto.Aprobacion or OperacionDeAdelanto.Rechazo);
    }
}
