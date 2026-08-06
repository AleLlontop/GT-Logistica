using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Usuarios;

namespace GT.IntegrationTests.Choferes;

public class CrearChoferValidacionesTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
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
    public async Task Rechaza_CuilDuplicado()
    {
        var transportista = await app.CrearTransportistaAsync();
        var persona = await app.CrearPersonaAsync(dni: "35111222");
        await app.CrearChoferAsync(persona.Id, transportista.Id, cuil: "20351112221");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/choferes", Alta(
            dni: "36111222", 
            cuil: "20351112221", // ya tomado por el chofer de arriba
            transportistaId: transportista.Id));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("cuil_duplicado", error!.Codigo);
        Assert.Equal("Ese CUIL ya está registrado para otro chofer.", error.Mensaje);
        Assert.Equal("cuil", error.Campo);
    }

    [Fact]
    public async Task Rechaza_MenorDeEdad()
    {
        var transportista = await app.CrearTransportistaAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        // Si el test se corre hoy, un nacido ayer hace 17 años es menor
        var hace17Anios = DateTime.Today.AddYears(-17).ToString("yyyy-MM-dd");

        var respuesta = await cliente.PostAsJsonAsync("/api/choferes", Alta(
            dni: "50111222", 
            cuil: "20501112225", // CUIL bien formado: lo que se rechaza es la edad, no el número
            fechaNacimiento: hace17Anios,
            transportistaId: transportista.Id));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("menor_de_edad", error!.Codigo);
        Assert.Equal("Un chofer tiene que ser mayor de 18 años.", error.Mensaje);
        Assert.Equal("fechaNacimiento", error.Campo);
    }

    /// <summary>
    /// El borde de FR-011: el día en que cumple 18 ya es mayor, así que el alta procede.
    /// </summary>
    [Fact]
    public async Task Acepta_AQuienCumple18Hoy()
    {
        var transportista = await app.CrearTransportistaAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var hace18Anios = DateTime.Today.AddYears(-18).ToString("yyyy-MM-dd");

        var respuesta = await cliente.PostAsJsonAsync("/api/choferes", Alta(
            dni: "51111222",
            cuil: "20511112223",
            fechaNacimiento: hace18Anios,
            transportistaId: transportista.Id));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    private record RespuestaError(string Codigo, string Mensaje, string? Campo);
}
