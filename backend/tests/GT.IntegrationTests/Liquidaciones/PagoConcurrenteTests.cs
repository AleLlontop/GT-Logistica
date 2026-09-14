using System.Net;
using System.Net.Http.Json;
using GT.Domain.Choferes;
using GT.Domain.Liquidaciones;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Liquidaciones;

/// <summary>
/// SC-007: dos órdenes de pago simultáneas. Lo cierra el <c>UPDATE</c> condicional sobre la fila de la
/// liquidación, que evalúa el importe contra el saldo <b>confirmado</b> (research §2). No se puede
/// verificar a mano en el mismo milisegundo.
/// </summary>
public class PagoConcurrenteTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Dos_OrdenesQueJuntasSuperanElSaldo_RegistranExactamenteUna()
    {
        for (var vuelta = 0; vuelta < 3; vuelta++)
        {
            var liquidacion = await LiquidacionDeAsync(155_000m);

            var respuestas = await PagarALaVezAsync(liquidacion.Id, 100_000m, 100_000m);

            Assert.Equal(1, respuestas.Count(respuesta => respuesta.StatusCode == HttpStatusCode.Created));

            var rechazada = respuestas.Single(respuesta => respuesta.StatusCode != HttpStatusCode.Created);
            Assert.Equal(HttpStatusCode.BadRequest, rechazada.StatusCode);
            Assert.Equal(
                "importe_supera_saldo",
                (await rechazada.Content.ReadFromJsonAsync<ErrorLiquidacionLeido>())!.Codigo);

            var releida = (await app.RecargarLiquidacionAsync(liquidacion.Id))!;
            Assert.Equal(100_000m, releida.ImportePagado);
            Assert.Single(releida.OrdenesDePago);
            Assert.True(releida.ImportePagado <= releida.ImporteTotal);
        }
    }

    [Fact]
    public async Task Dos_OrdenesQueJuntasIgualanElTotal_LaDejanPagada()
    {
        var liquidacion = await LiquidacionDeAsync(155_000m);

        var respuestas = await PagarALaVezAsync(liquidacion.Id, 77_500m, 77_500m);

        Assert.All(respuestas, respuesta => Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode));

        var releida = (await app.RecargarLiquidacionAsync(liquidacion.Id))!;
        Assert.Equal(EstadoLiquidacion.Pagada, releida.Estado);
        Assert.Equal(155_000m, releida.ImportePagado);
        Assert.Single(releida.Cambios, cambio => cambio.Operacion == OperacionDeLiquidacion.Pagada);
    }

    private async Task<Liquidacion> LiquidacionDeAsync(decimal total)
    {
        var (clienteId, fletero) = await app.EscenarioBaseAsync();
        var viaje = await app.ViajeDeAsync(clienteId, fletero.Id, total);

        return await app.CrearLiquidacionAsync(fletero.Id, [viaje]);
    }

    private async Task<HttpResponseMessage[]> PagarALaVezAsync(int id, decimal primero, decimal segundo)
    {
        var operadorA = await app.CrearClienteAutenticadoAsync();
        var operadorB = await app.CrearClienteAutenticadoAsync();
        var hoy = FechaHoyArgentina.Hoy().ToString("yyyy-MM-dd");

        return await Task.WhenAll(
            operadorA.PostAsJsonAsync(
                $"/api/liquidaciones/{id}/ordenes-de-pago",
                new { fechaPago = hoy, importe = primero, confirmado = true }),
            operadorB.PostAsJsonAsync(
                $"/api/liquidaciones/{id}/ordenes-de-pago",
                new { fechaPago = hoy, importe = segundo, confirmado = true }));
    }
}
