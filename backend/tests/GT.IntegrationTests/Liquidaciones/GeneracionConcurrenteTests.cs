using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Liquidaciones;

/// <summary>
/// SC-002: <b>dos operadores generan a la vez liquidaciones que comparten un viaje</b>.
///
/// Esto no lo puede verificar una persona en el mismo milisegundo, y el Principio IV obliga a declararlo.
/// Lo cierra <c>IX_LiquidacionViajes_ViajeVigente</c>: la consulta previa deja pasar a los dos y el índice
/// corta al segundo (research §1).
/// </summary>
public class GeneracionConcurrenteTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Dos_GeneracionesConUnViajeEnComun_CreanExactamenteUna()
    {
        // Varias vueltas: una sola podría no cruzarse nunca y pasar por la consulta previa.
        for (var vuelta = 0; vuelta < 3; vuelta++)
        {
            var (clienteId, fletero) = await app.EscenarioBaseAsync();

            var compartido = await app.ViajeDeAsync(clienteId, fletero.Id, 100_000m);
            var propioDeA = await app.ViajeDeAsync(clienteId, fletero.Id, 50_000m);
            var propioDeB = await app.ViajeDeAsync(clienteId, fletero.Id, 70_000m);

            var operadorA = await app.CrearClienteAutenticadoAsync();
            var operadorB = await app.CrearClienteAutenticadoAsync();

            var respuestas = await Task.WhenAll(
                operadorA.PostAsJsonAsync(
                    "/api/liquidaciones",
                    DatosDePruebaLiquidaciones.CuerpoDeGeneracion(fletero.Id, [compartido.Id, propioDeA.Id])),
                operadorB.PostAsJsonAsync(
                    "/api/liquidaciones",
                    DatosDePruebaLiquidaciones.CuerpoDeGeneracion(fletero.Id, [compartido.Id, propioDeB.Id])));

            Assert.Equal(1, respuestas.Count(respuesta => respuesta.StatusCode == HttpStatusCode.Created));

            var rechazada = respuestas.Single(respuesta => respuesta.StatusCode != HttpStatusCode.Created);
            Assert.Equal(HttpStatusCode.Conflict, rechazada.StatusCode);

            var error = await rechazada.Content.ReadFromJsonAsync<ErrorLiquidacionLeido>();
            Assert.Equal("viaje_ya_liquidado", error!.Codigo);
            Assert.Contains(error.Viajes!, viaje => viaje.Id == compartido.Id);

            // Lo que hace que la garantía sea real: una sola liquidación, con sus dos vínculos y su única
            // entrada de historial, y nada huérfano de la rechazada.
            var (liquidaciones, vinculos, cambios) = await app.ConAlcanceAsync(async contexto =>
            {
                var ids = await contexto.Liquidaciones
                    .Where(liquidacion => liquidacion.TransportistaId == fletero.Id)
                    .Select(liquidacion => liquidacion.Id)
                    .ToListAsync();

                return (
                    ids,
                    await contexto.LiquidacionViajes.CountAsync(vinculo =>
                        vinculo.ViajeId == compartido.Id ||
                        vinculo.ViajeId == propioDeA.Id ||
                        vinculo.ViajeId == propioDeB.Id),
                    await contexto.CambiosDeLiquidacion.CountAsync(cambio => ids.Contains(cambio.LiquidacionId)));
            });

            Assert.Single(liquidaciones);
            Assert.Equal(2, vinculos);
            Assert.Equal(1, cambios);
        }
    }
}
