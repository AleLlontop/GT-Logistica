using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;
using GT.IntegrationTests.Usuarios;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Choferes;

/// <summary>
/// Alta de un chofer reutilizando el padrón de personas del Módulo 2 (FR-006, US2 esc. 1 y 3).
/// </summary>
public class CrearChoferTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
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

    /// <summary>
    /// El camino central de la historia: un DNI que no está en el padrón crea la persona y el chofer
    /// en la misma operación (US2 esc. 1).
    /// </summary>
    [Fact]
    public async Task Registra_UnChofer_CreandoLaPersona_CuandoElDniNoEstaEnElPadron()
    {
        var transportista = await app.CrearTransportistaAsync();
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/choferes", Alta(
            nombre: "Ramona",
            apellido: "Gómez",
            dni: "31111222",
            cuil: "27311112223",
            transportistaId: transportista.Id));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var creado = await respuesta.Content.ReadFromJsonAsync<ChoferLeido>();
        Assert.NotNull(creado);
        Assert.Equal("Gómez", creado.Apellido);
        Assert.Equal("Ramona", creado.Nombre);
        Assert.Equal("31111222", creado.Dni);
        Assert.True(creado.Activo);
        Assert.Equal(transportista.Id, creado.Transportista.Id);

        // Sin documentación cargada no está "en regla": es una cuarta situación (FR-028).
        Assert.Equal("sinDocumentacion", creado.EstadoDocumentacion);
        Assert.Empty(creado.Documentos);

        // No reutilizó a nadie: la persona la creó esta misma alta.
        Assert.False(creado.ReutilizoPersona);

        var personasConEseDni = await app.ConAlcanceAsync(contexto =>
            contexto.Personas.CountAsync(persona => persona.Dni == "31111222"));

        Assert.Equal(1, personasConEseDni);
        Assert.NotEqual(0, creado.PersonaId);
    }

    [Fact]
    public async Task ReutilizaPersona_SiElDniYaExiste()
    {
        var transportista = await app.CrearTransportistaAsync();
        var personaPreexistente = await app.CrearPersonaAsync(dni: "40111222", activa: true);

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/choferes", Alta(
            dni: "40111222",
            cuil: "20401112228",
            transportistaId: transportista.Id));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var creado = await respuesta.Content.ReadFromJsonAsync<ChoferLeido>();
        Assert.NotNull(creado);

        // Lo que la historia exige: el chofer cuelga de la persona que ya estaba, no de una nueva.
        Assert.Equal(personaPreexistente.Id, creado.PersonaId);
        Assert.True(creado.ReutilizoPersona);

        // Y el padrón no se duplicó: sigue habiendo una sola persona con ese DNI.
        var personasConEseDni = await app.ConAlcanceAsync(contexto =>
            contexto.Personas.CountAsync(persona => persona.Dni == "40111222"));

        Assert.Equal(1, personasConEseDni);
    }

    /// <summary>
    /// El DNI se normaliza antes de buscar en el padrón, así que escribirlo con puntos encuentra a
    /// la misma persona (FR-025).
    /// </summary>
    [Fact]
    public async Task ReutilizaPersona_AunqueElDniVengaConPuntos()
    {
        var transportista = await app.CrearTransportistaAsync();
        var personaPreexistente = await app.CrearPersonaAsync(dni: "42111222");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/choferes", Alta(
            dni: "42.111.222",
            cuil: "20421112224",
            transportistaId: transportista.Id));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var creado = await respuesta.Content.ReadFromJsonAsync<ChoferLeido>();
        Assert.Equal(personaPreexistente.Id, creado!.PersonaId);
        Assert.Equal("42111222", creado.Dni);
    }

    [Fact]
    public async Task Rechaza_SiLaPersona_YaEsChofer()
    {
        var transportista = await app.CrearTransportistaAsync();
        var persona = await app.CrearPersonaAsync(dni: "41111222");
        await app.CrearChoferAsync(persona.Id, transportista.Id, cuil: "20411112226");

        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/choferes", Alta(
            dni: "41111222",
            cuil: "20301112220",
            transportistaId: transportista.Id));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("dni_duplicado", error!.Codigo);
        Assert.Equal("Esa persona ya está registrada como chofer.", error.Mensaje);
        Assert.Equal("dni", error.Campo);
    }

    private record TransportistaDelChofer(int Id, string Nombre);

    private record ChoferLeido(
        int Id,
        string Apellido,
        string Nombre,
        string Dni,
        string Cuil,
        TransportistaDelChofer Transportista,
        bool Activo,
        string EstadoDocumentacion,
        int PersonaId,
        IReadOnlyList<object> Documentos,
        bool ReutilizoPersona);

    private record RespuestaError(string Codigo, string Mensaje, string? Campo);
}
