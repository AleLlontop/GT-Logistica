using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Choferes;

public class CrearChoferTransportistaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private static object Alta(
        string nombre = "Juan",
        string apellido = "Pérez",
        string dni = "30111222",
        string cuil = "20301112220",
        string fechaNacimiento = "1990-05-17",
        string telefono = "11-5555-5555",
        string email = "juan@gt.com.ar",
        int transportistaId = 1) => new
        {
            nombre,
            apellido,
            dni,
            cuil,
            fechaNacimiento,
            telefono,
            email,
            transportistaId
        };

    [Fact]
    public async Task Rechaza_TransportistaInexistente()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/choferes", Alta(
            dni: "39111222", 
            cuil: "20391112224", // CUIL válido
            transportistaId: 99999));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("transportista_inexistente", error!.Codigo);
    }

    [Fact]
    public async Task Rechaza_TransportistaInactivo()
    {
        var transportistaInactivo = await app.CrearTransportistaAsync(activo: false);
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/choferes", Alta(
            dni: "38111222", 
            cuil: "20381112226", // CUIL válido
            transportistaId: transportistaInactivo.Id));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("transportista_inexistente", error!.Codigo);
    }

    private record RespuestaError(string Codigo, string Mensaje, string? Campo);
}
