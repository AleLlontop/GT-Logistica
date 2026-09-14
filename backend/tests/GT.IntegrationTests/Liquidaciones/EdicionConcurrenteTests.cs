using System.Net;
using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Liquidaciones;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Liquidaciones;

/// <summary>
/// Las carreras de la edición (FR-048, research §2): dos ediciones de la misma versión, y una edición
/// contra un pago. No se pueden verificar a mano en el mismo milisegundo.
/// </summary>
public class EdicionConcurrenteTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Dos_EdicionesDeLaMismaVersion_AplicanExactamenteUna()
    {
        for (var vuelta = 0; vuelta < 3; vuelta++)
        {
            var (liquidacion, viajes, libre) = await EscenarioAsync();

            var editorA = await app.CrearClienteAutenticadoAsync();
            var editorB = await app.CrearClienteAutenticadoAsync();

            var respuestas = await Task.WhenAll(
                // A quita un viaje; B agrega otro. Las dos sobre la versión 0.
                editorA.PutAsJsonAsync(
                    $"/api/liquidaciones/{liquidacion.Id}",
                    new { viajeIds = new[] { viajes[1], viajes[2] }, version = 0 }),
                editorB.PutAsJsonAsync(
                    $"/api/liquidaciones/{liquidacion.Id}",
                    new { viajeIds = new[] { viajes[0], viajes[1], viajes[2], libre }, version = 0 }));

            Assert.Equal(1, respuestas.Count(respuesta => respuesta.StatusCode == HttpStatusCode.OK));

            var rechazada = respuestas.Single(respuesta => respuesta.StatusCode != HttpStatusCode.OK);
            Assert.Equal(HttpStatusCode.Conflict, rechazada.StatusCode);
            Assert.Equal(
                "liquidacion_modificada",
                (await rechazada.Content.ReadFromJsonAsync<ErrorLiquidacionLeido>())!.Codigo);

            var releida = (await app.RecargarLiquidacionAsync(liquidacion.Id))!;
            Assert.Equal(1, releida.Version);
            await AssertTotalIgualALaSumaAsync(liquidacion.Id);
        }
    }

    /// <summary>
    /// Una edición y un pago que se cruzan nunca dejan editada una liquidación que ya tenía pagos: o la
    /// edición llega primero y se aplica sin pagos, o el pago llega primero y la edición se rechaza por eso.
    /// </summary>
    [Fact]
    public async Task Una_EdicionYUnPagoALaVez_NuncaDejanEditadaUnaLiquidacionConPagos()
    {
        for (var vuelta = 0; vuelta < 3; vuelta++)
        {
            var (liquidacion, viajes, _) = await EscenarioAsync();

            var editor = await app.CrearClienteAutenticadoAsync();
            var pagador = await app.CrearClienteAutenticadoAsync();

            var respuestas = await Task.WhenAll(
                editor.PutAsJsonAsync(
                    $"/api/liquidaciones/{liquidacion.Id}",
                    new { viajeIds = new[] { viajes[1], viajes[2] }, version = 0 }),
                pagador.PostAsJsonAsync(
                    $"/api/liquidaciones/{liquidacion.Id}/ordenes-de-pago",
                    new { fechaPago = FechaHoyArgentina.Hoy().ToString("yyyy-MM-dd"), importe = 10_000m, confirmado = true }));

            var (edicion, pago) = (respuestas[0], respuestas[1]);

            Assert.Equal(HttpStatusCode.Created, pago.StatusCode);

            if (edicion.StatusCode != HttpStatusCode.OK)
            {
                var error = (await edicion.Content.ReadFromJsonAsync<ErrorLiquidacionLeido>())!;
                Assert.Equal("liquidacion_no_editable", error.Codigo);
                Assert.Equal("conOrdenesDePago", error.Motivo);
            }

            var releida = (await app.RecargarLiquidacionAsync(liquidacion.Id))!;
            Assert.Equal(10_000m, releida.ImportePagado);
            await AssertTotalIgualALaSumaAsync(liquidacion.Id);

            if (edicion.StatusCode == HttpStatusCode.OK)
            {
                // Si la edición se aplicó, fue antes del pago: la orden es posterior a la entrada.
                var entrada = releida.Cambios.Single(cambio => cambio.Operacion == OperacionDeLiquidacion.Edicion);
                Assert.True(entrada.OcurridoEn <= Assert.Single(releida.OrdenesDePago).RegistradaEn);
            }
        }
    }

    private async Task<(Liquidacion Liquidacion, int[] Viajes, int Libre)> EscenarioAsync()
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();

        var viajes = new[]
        {
            await app.ViajeDeAsync(clienteId, fletero.Id, 120_000m),
            await app.ViajeDeAsync(clienteId, fletero.Id, 95_000m),
            await app.ViajeDeAsync(clienteId, fletero.Id, 140_000m),
        };

        var libre = await app.ViajeDeAsync(clienteId, fletero.Id, 80_000m);
        var liquidacion = await app.CrearLiquidacionAsync(fletero.Id, viajes);

        return (liquidacion, [.. viajes.Select(viaje => viaje.Id)], libre.Id);
    }

    private Task AssertTotalIgualALaSumaAsync(int id) =>
        app.EnLaBaseAsync(async contexto =>
        {
            var total = await contexto.Liquidaciones
                .Where(liquidacion => liquidacion.Id == id)
                .Select(liquidacion => liquidacion.ImporteTotal)
                .SingleAsync();

            var suma = await contexto.LiquidacionViajes
                .Where(vinculo => vinculo.LiquidacionId == id && vinculo.Vigente)
                .SumAsync(vinculo => vinculo.Viaje!.Importe);

            Assert.Equal(suma, total);
        });
}
