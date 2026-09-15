using System.Net;
using System.Net.Http.Json;
using GT.Domain.Adelantos;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Adelantos;

/// <summary>
/// Dos anulaciones confirmadas del mismo aprobado, a la vez (FR-032, research §2): exactamente una se
/// aplica, y nunca quedan dos anulaciones con motivos distintos.
/// </summary>
public class AnulacionConcurrenteTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Dos_AnulacionesALaVez_DejanUnaSolaAnulacion()
    {
        for (var vuelta = 0; vuelta < 3; vuelta++)
        {
            var persona = await app.EmpleadoAsync();
            var adelanto = await app.CrearAdelantoAsync(persona.Id, EstadoAdelanto.Aprobado);

            var primero = await app.CrearClienteAutenticadoAsync();
            var segundo = await app.CrearClienteAutenticadoAsync();

            var respuestas = await Task.WhenAll(
                primero.AnularAsync(adelanto.Id, "Persona equivocada.", confirmado: true),
                segundo.AnularAsync(adelanto.Id, "Importe mal tipeado.", confirmado: true));

            Assert.Equal(1, respuestas.Count(respuesta => respuesta.StatusCode == HttpStatusCode.OK));

            var perdedora = Assert.Single(respuestas, respuesta => respuesta.StatusCode != HttpStatusCode.OK);
            Assert.Equal(HttpStatusCode.Conflict, perdedora.StatusCode);

            var error = (await perdedora.Content.ReadFromJsonAsync<ErrorAdelantoLeido>())!;
            Assert.Equal("adelanto_no_anulable", error.Codigo);
            Assert.Equal("anulado", error.Estado);

            var releido = (await app.RecargarAdelantoAsync(adelanto.Id))!;
            Assert.Equal(EstadoAdelanto.Anulado, releido.Estado);
            Assert.Single(releido.Cambios, cambio => cambio.Operacion is OperacionDeAdelanto.Anulacion);
        }
    }
}
