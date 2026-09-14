using System.Net;
using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Liquidaciones;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Liquidaciones;

/// <summary>
/// Una anulación y un pago que se cruzan (FR-053, research §2): las dos empiezan con un <c>UPDATE</c>
/// condicional sobre la misma fila, así que exactamente una gana. Nunca queda un pago sobre una anulada.
/// </summary>
public class AnulacionConcurrenteTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Una_AnulacionYUnPagoALaVez_NuncaDejanUnPagoSobreUnaAnulada()
    {
        for (var vuelta = 0; vuelta < 3; vuelta++)
        {
            var (clienteId, fletero) = await app.EscenarioBaseAsync();
            var viaje = await app.ViajeDeAsync(clienteId, fletero.Id, 100_000m);
            var liquidacion = await app.CrearLiquidacionAsync(fletero.Id, [viaje]);

            var anulador = await app.CrearClienteAutenticadoAsync();
            var pagador = await app.CrearClienteAutenticadoAsync();

            var respuestas = await Task.WhenAll(
                anulador.PostAsJsonAsync(
                    $"/api/liquidaciones/{liquidacion.Id}/anulacion",
                    new { motivo = "Generada por error.", confirmado = true }),
                pagador.PostAsJsonAsync(
                    $"/api/liquidaciones/{liquidacion.Id}/ordenes-de-pago",
                    new { fechaPago = FechaHoyArgentina.Hoy().ToString("yyyy-MM-dd"), importe = 10_000m, confirmado = true }));

            var (anulacion, pago) = (respuestas[0], respuestas[1]);

            var exitos = (anulacion.StatusCode == HttpStatusCode.OK ? 1 : 0) +
                (pago.StatusCode == HttpStatusCode.Created ? 1 : 0);

            Assert.Equal(1, exitos);

            var releida = (await app.RecargarLiquidacionAsync(liquidacion.Id))!;

            if (releida.Estado is EstadoLiquidacion.Anulada)
            {
                Assert.Empty(releida.OrdenesDePago);
                Assert.Equal(0m, releida.ImportePagado);
                Assert.Equal(
                    "liquidacion_no_pagable",
                    (await pago.Content.ReadFromJsonAsync<ErrorLiquidacionLeido>())!.Codigo);
            }
            else
            {
                Assert.Single(releida.OrdenesDePago);
                Assert.All(releida.Viajes, vinculo => Assert.True(vinculo.Vigente));
                Assert.Equal(
                    "liquidacion_no_anulable",
                    (await anulacion.Content.ReadFromJsonAsync<ErrorLiquidacionLeido>())!.Codigo);
            }
        }
    }
}
