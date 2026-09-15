using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Adelantos;

/// <summary>
/// Las dos rutas literales conviven con <c>/api/adelantos/{id:int}</c>. Sin la restricción de tipo quedarían
/// inalcanzables, y eso <b>no falla ni al compilar ni al arrancar</b>: falla al pedirlas (convención [005]).
/// </summary>
public class RutasAdelantosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task Beneficiarios_RespondeSuPropioCuerpo()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync("/api/adelantos/beneficiarios?tipo=empleado");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.NotNull((await respuesta.Content.ReadFromJsonAsync<BeneficiariosLeidos>())!.Personas);
    }

    [Fact]
    public async Task Personas_RespondeSuPropioCuerpo()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.GetAsync("/api/adelantos/personas");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.NotNull(await respuesta.Content.ReadFromJsonAsync<List<PersonaResumenLeida>>());
    }
}
