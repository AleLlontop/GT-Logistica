using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Caja;

/// <summary>
/// Las rutas literales del prefijo <c>/api/caja</c> son alcanzables junto a <c>/{id:int}</c>. Sin la
/// restricción de tipo quedarían capturadas por la de identificador y responderían un error de conversión,
/// sin fallar al compilar ni al arrancar (convención [005]).
/// </summary>
public class RutasCajaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Abierta_RespondeSuPropioCuerpo_O204()
    {
        var (_, cliente) = await app.EmpleadoAsync();

        Assert.Equal(HttpStatusCode.NoContent, (await cliente.GetAsync("/api/caja/abierta")).StatusCode);

        await cliente.AbrirCajaAsync(10_000m);

        var respuesta = await cliente.GetAsync("/api/caja/abierta");
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal("abierta", (await respuesta.Content.ReadFromJsonAsync<CajaDetalleLeida>())!.Estado);
    }

    [Fact]
    public async Task Facturas_Pendientes_RespondeUnaLista()
    {
        var (_, cliente) = await app.EmpleadoAsync();

        var respuesta = await cliente.GetAsync("/api/caja/facturas-pendientes");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.NotNull(await respuesta.Content.ReadFromJsonAsync<List<OpcionLeida>>());
    }

    [Fact]
    public async Task Ordenes_DePago_RespondeUnaLista()
    {
        var (_, cliente) = await app.EmpleadoAsync();

        var respuesta = await cliente.GetAsync("/api/caja/ordenes-de-pago");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.NotNull(await respuesta.Content.ReadFromJsonAsync<List<OpcionLeida>>());
    }
}
