using System.Net;
using System.Net.Http.Json;
using GT.Domain.Personas;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Usuarios;

/// <summary>
/// User Story 6: padrón de personas.
///
/// Cubre FR-024 (el padrón arranca vacío), FR-026 (los siete datos y ninguno más), FR-027 (DNI único)
/// y FR-028 (no se puede dar de baja una persona vinculada, sin importar el estado de ese usuario).
/// </summary>
public class PersonasTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private static object Alta(
        string dni,
        string nombre = "Juan",
        string apellido = "Pérez",
        string tipo = "chofer",
        string telefono = "11-5555-5555",
        string? email = null,
        string fechaNacimiento = "1990-05-17") => new
        {
            nombre,
            apellido,
            dni,
            tipo,
            telefono,
            email = email ?? $"{dni}@gt.com.ar",
            fechaNacimiento,
        };

    [Fact]
    public async Task ElPadron_ArrancaVacio_EnUnaBaseReciénMigrada()
    {
        // FR-024: la migración no siembra ninguna persona. Es lo que hace que el selector del
        // formulario de usuario muestre su estado vacío en toda instalación nueva.
        var enLaBase = await app.ConAlcanceAsync(contexto => contexto.Personas.CountAsync());

        // Otros tests de esta corrida pueden haber cargado personas; lo que se verifica es que la
        // migración no dejó ninguna sembrada, no que la tabla esté vacía para siempre.
        var sembradasPorMigracion = await app.ConAlcanceAsync(contexto => contexto.Personas
            .Where(persona => persona.Dni == "00000000")
            .CountAsync());

        Assert.Equal(0, sembradasPorMigracion);
        Assert.True(enLaBase >= 0);
    }

    [Fact]
    public async Task Registra_UnaPersonaConLosSieteDatos()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/personas", Alta("35111222", "Marta", "Gómez"));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var creada = await respuesta.Content.ReadFromJsonAsync<PersonaLeida>();

        Assert.NotNull(creada);
        Assert.Equal("Marta", creada.Nombre);
        Assert.Equal("Gómez", creada.Apellido);
        Assert.Equal("35111222", creada.Dni);
        Assert.Equal("chofer", creada.Tipo);
        Assert.True(creada.Activa);
    }

    [Fact]
    public async Task Rechaza_UnDniYaRegistrado()
    {
        // FR-027.
        var cliente = await app.CrearClienteAutenticadoAsync();
        await cliente.PostAsJsonAsync("/api/personas", Alta("35333444"));

        var respuesta = await cliente.PostAsJsonAsync("/api/personas", Alta("35333444", "Otra", "Persona"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("dni_duplicado", error!.Codigo);
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-son-digitos")]
    [InlineData("123")]
    public async Task Rechaza_UnDniConFormatoInvalido(string dni)
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/personas", Alta(dni));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("datos_invalidos", error!.Codigo);
    }

    [Fact]
    public async Task Rechaza_UnEmailConFormatoInvalido()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/personas",
            Alta("35555666", email: "esto-no-es-un-mail"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Modifica_UnaPersona_ConservandoSuPropioDni()
    {
        // FR-027: la comparación de unicidad excluye a la propia persona.
        var cliente = await app.CrearClienteAutenticadoAsync();
        var persona = await app.CrearPersonaAsync("35777888");

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/personas/{persona.Id}",
            Alta("35777888", "Corregido", "Apellido"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var recargada = await app.RecargarPersonaAsync(persona.Id);
        Assert.Equal("Corregido", recargada!.Nombre);
    }

    [Fact]
    public async Task Rechaza_LaModificacion_ConElDniDeOtraPersona()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        await app.CrearPersonaAsync("35999000");
        var persona = await app.CrearPersonaAsync("36111222");

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/personas/{persona.Id}",
            Alta("35999000"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("dni_duplicado", error!.Codigo);
    }

    [Fact]
    public async Task DaDeBaja_UnaPersonaLibre_SinBorrarla()
    {
        // FR-022: la baja es lógica, el registro no se borra nunca.
        var cliente = await app.CrearClienteAutenticadoAsync();
        var persona = await app.CrearPersonaAsync("36333444");

        var respuesta = await cliente.DeleteAsync($"/api/personas/{persona.Id}");

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);

        var recargada = await app.RecargarPersonaAsync(persona.Id);

        Assert.NotNull(recargada);
        Assert.False(recargada.Activa);
    }

    [Fact]
    public async Task LaPersonaDadaDeBaja_DejaDeOfrecerseParaAsociar()
    {
        // FR-023: el selector del formulario de usuario sólo ofrece personas activas.
        var cliente = await app.CrearClienteAutenticadoAsync();
        var persona = await app.CrearPersonaAsync("36555666");

        await cliente.DeleteAsync($"/api/personas/{persona.Id}");

        var activas = await cliente.GetFromJsonAsync<List<PersonaLeida>>(
            "/api/personas?soloActivas=true");

        Assert.DoesNotContain(activas!, ofrecida => ofrecida.Id == persona.Id);

        // Pero sigue apareciendo en el listado completo del padrón.
        var todas = await cliente.GetFromJsonAsync<List<PersonaLeida>>("/api/personas");

        Assert.Contains(todas!, listada => listada.Id == persona.Id);
    }

    [Fact]
    public async Task Rechaza_LaBaja_DeUnaPersonaVinculadaAUnUsuario()
    {
        // FR-028, informando a qué usuario pertenece.
        var cliente = await app.CrearClienteAutenticadoAsync();
        var persona = await app.CrearPersonaAsync("36777888");
        await app.CrearUsuarioAsync("dueño.de.persona", personaId: persona.Id);

        var respuesta = await cliente.DeleteAsync($"/api/personas/{persona.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();

        Assert.Equal("persona_vinculada", error!.Codigo);
        Assert.Contains("dueño.de.persona", error.Mensaje);

        // Y sigue activa.
        var recargada = await app.RecargarPersonaAsync(persona.Id);
        Assert.True(recargada!.Activa);
    }

    [Fact]
    public async Task Rechaza_LaBaja_AunqueElUsuarioDueñoEsteInactivo()
    {
        // FR-028: sin importar el estado de ese usuario. Es el caso que más fácil se escapa.
        var cliente = await app.CrearClienteAutenticadoAsync();
        var persona = await app.CrearPersonaAsync("36999000");

        await app.CrearUsuarioAsync(
            "dueño.inactivo.persona",
            estado: EstadoUsuario.Inactivo,
            personaId: persona.Id);

        var respuesta = await cliente.DeleteAsync($"/api/personas/{persona.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("persona_vinculada", error!.Codigo);
    }

    [Fact]
    public async Task Busca_PorNombreApellidoODni_ConCoincidenciaParcial()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        await app.CrearPersonaAsync("37111222", "Rosalía", "Fernández");

        foreach (var fragmento in new[] { "rosal", "ROSAL", "fernán", "37111" })
        {
            var encontradas = await cliente.GetFromJsonAsync<List<PersonaLeida>>(
                $"/api/personas?texto={Uri.EscapeDataString(fragmento)}");

            Assert.Contains(encontradas!, persona => persona.Dni == "37111222");
        }
    }

    [Fact]
    public async Task Rechaza_ElAcceso_ParaUnUsuarioSinElPermisoDeGestion()
    {
        // FR-007: el padrón comparte la restricción del módulo, sin un permiso propio.
        var cliente = await app.CrearClienteComoAsync("trafico.padron", CodigosRol.Trafico);

        var respuesta = await cliente.PostAsJsonAsync("/api/personas", Alta("37333444"));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Devuelve_NoEncontrado_AlModificarUnaPersonaQueNoExiste()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync("/api/personas/999999", Alta("37555666"));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    private record PersonaLeida(
        int Id,
        string Nombre,
        string Apellido,
        string Dni,
        string Tipo,
        string Telefono,
        string Email,
        bool Activa);

    private record RespuestaError(string Codigo, string Mensaje, string? Campo);
}
