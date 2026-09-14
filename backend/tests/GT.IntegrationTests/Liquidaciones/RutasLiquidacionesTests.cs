using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Liquidaciones;

/// <summary>
/// Las dos rutas literales conviven con <c>/api/liquidaciones/{id:int}</c>. Sin la restricción de tipo
/// quedarían inalcanzables, y eso <b>no falla ni al compilar ni al arrancar</b>: falla al pedirlas
/// (convención [005], research §12.3).
/// </summary>
public class RutasLiquidacionesTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Transportistas_RespondeSuPropioCuerpo()
    {
        await app.EscenarioBaseAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync("/api/liquidaciones/transportistas");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<TransportistasLiquidablesLeidos>();
        Assert.True(cuerpo!.EmpresaEmisoraConfigurada);
    }

    [Fact]
    public async Task Disponibles_RespondeSuPropioCuerpo()
    {
        var (_, fletero) = await app.EscenarioBaseAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync(DatosDePruebaLiquidaciones.RutaDeDisponibles(fletero.Id));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.NotNull(await respuesta.Content.ReadFromJsonAsync<List<ViajeDisponibleLeido>>());
    }
}
