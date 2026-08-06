using System.Net;
using System.Net.Http.Json;
using GT.Domain.Usuarios;
using GT.Infrastructure.DatosIniciales;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Usuarios;

/// <summary>
/// User Story 1: alta de usuarios.
///
/// Cubre FR-001 a FR-005, FR-008 y FR-020, y el criterio SC-002: todo rechazo tiene que identificar
/// la causa exacta, no fallar de forma genérica.
/// </summary>
public class CrearUsuarioTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private static object AltaValida(
        string username = "jperez",
        string? email = null,
        string password = DatosDePrueba.PasswordValida,
        string estado = "activo",
        string[]? roles = null,
        int? personaId = null) => new
        {
            username,
            email = email ?? $"{username}@gt.com.ar",
            password,
            estado,
            roles = roles ?? [CodigosRol.Trafico],
            personaId,
        };

    [Fact]
    public async Task Crea_UnUsuarioValido_ConFechaAltaDeHoy_YSinUltimoAcceso()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/usuarios", AltaValida("alta.valida"));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);

        var creado = await respuesta.Content.ReadFromJsonAsync<RespuestaUsuario>();

        Assert.NotNull(creado);
        Assert.Equal("alta.valida", creado.Username);
        Assert.Equal("activo", creado.Estado);
        Assert.Null(creado.UltimoAcceso);
        Assert.Equal(DateTime.UtcNow.Date, creado.FechaAlta.Date);
        Assert.Single(creado.Roles);
    }

    [Fact]
    public async Task Crea_ElUsuario_QuedandoHabilitadoParaIngresarDeInmediato()
    {
        // SC-001: el 100% de los usuarios creados queda disponible para autenticarse, sin
        // intervención técnica.
        var cliente = await app.CrearClienteAutenticadoAsync();

        await cliente.PostAsJsonAsync("/api/usuarios", AltaValida("ingresa.ya"));

        var nuevoCliente = app.CrearCliente();

        var ingreso = await nuevoCliente.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "ingresa.ya", password = DatosDePrueba.PasswordValida });

        Assert.Equal(HttpStatusCode.OK, ingreso.StatusCode);
    }

    [Fact]
    public async Task Rechaza_UnUsernameDuplicado_IdentificandoCualEsElDuplicado()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/usuarios",
            AltaValida(SembradorInicial.UsernameAdministrador, email: "otro.mail@gt.com.ar"));

        await AssertErrorAsync(respuesta, HttpStatusCode.BadRequest, "username_duplicado");
    }

    [Fact]
    public async Task Rechaza_UnUsernameQueSoloDifiereEnMayusculas()
    {
        // FR-020: la normalización impide que "Admin" y "admin" convivan como usuarios distintos.
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/usuarios",
            AltaValida("  ADMIN  ", email: "mayusculas@gt.com.ar"));

        await AssertErrorAsync(respuesta, HttpStatusCode.BadRequest, "username_duplicado");
    }

    [Fact]
    public async Task Rechaza_UnEmailDuplicado_AunqueDifieraEnMayusculas()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        await cliente.PostAsJsonAsync("/api/usuarios", AltaValida("dueño.mail", email: "repetido@gt.com.ar"));

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/usuarios",
            AltaValida("otro.usuario", email: "  REPETIDO@GT.COM.AR "));

        await AssertErrorAsync(respuesta, HttpStatusCode.BadRequest, "email_duplicado");
    }

    [Fact]
    public async Task Rechaza_UnaPasswordDeMenosDeOchoCaracteres()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/usuarios",
            AltaValida("password.corta", password: "1234567"));

        await AssertErrorAsync(respuesta, HttpStatusCode.BadRequest, "datos_invalidos");
    }

    [Fact]
    public async Task Rechaza_UnEmailConFormatoInvalido()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/usuarios",
            AltaValida("mail.invalido", email: "esto-no-es-un-mail"));

        await AssertErrorAsync(respuesta, HttpStatusCode.BadRequest, "datos_invalidos");
    }

    [Fact]
    public async Task Rechaza_ElAlta_CuandoNoHayNingunRolMarcado()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/usuarios",
            AltaValida("sin.roles", roles: []));

        await AssertErrorAsync(respuesta, HttpStatusCode.BadRequest, "sin_roles");
    }

    [Fact]
    public async Task Rechaza_UnaPersonaYaVinculadaAOtroUsuario()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var persona = await app.CrearPersonaAsync("30111222");
        await app.CrearUsuarioAsync("dueño.persona", personaId: persona.Id);

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/usuarios",
            AltaValida("quiere.persona", personaId: persona.Id));

        await AssertErrorAsync(respuesta, HttpStatusCode.BadRequest, "persona_ya_vinculada");
    }

    [Fact]
    public async Task Rechaza_UnaPersonaVinculada_AunqueEseUsuarioEsteInactivo()
    {
        // FR-008: la persona sigue ocupada cualquiera sea el estado del usuario que la tiene. La
        // única forma de liberarla es desasociarla explícitamente.
        var cliente = await app.CrearClienteAutenticadoAsync();
        var persona = await app.CrearPersonaAsync("30333444");

        await app.CrearUsuarioAsync(
            "dueño.inactivo",
            estado: EstadoUsuario.Inactivo,
            personaId: persona.Id);

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/usuarios",
            AltaValida("quiere.la.persona", personaId: persona.Id));

        await AssertErrorAsync(respuesta, HttpStatusCode.BadRequest, "persona_ya_vinculada");
    }

    [Fact]
    public async Task Rechaza_UnaPersonaDadaDeBaja()
    {
        // FR-023: sólo se ofrecen y se aceptan personas activas.
        var cliente = await app.CrearClienteAutenticadoAsync();
        var persona = await app.CrearPersonaAsync("30555666", activa: false);

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/usuarios",
            AltaValida("quiere.baja", personaId: persona.Id));

        await AssertErrorAsync(respuesta, HttpStatusCode.BadRequest, "persona_inexistente");
    }

    [Fact]
    public async Task Devuelve_LaPersonaAsociada_EnLaRespuestaDelAlta()
    {
        // El contrato dice que el alta devuelve `UsuarioDetalle`, que incluye la persona. Sin este
        // test, la respuesta puede decir `persona: null` sobre un usuario que sí quedó asociado
        // —porque el DTO lee la navegación y nadie la cargó— y sólo se nota operando la aplicación.
        var cliente = await app.CrearClienteAutenticadoAsync();
        var persona = await app.CrearPersonaAsync("29123456", "Marta", "Gómez");

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/usuarios",
            AltaValida("devuelve.persona", personaId: persona.Id));

        var creado = await respuesta.Content.ReadFromJsonAsync<RespuestaUsuarioConPersona>();

        Assert.NotNull(creado?.Persona);
        Assert.Equal("Marta", creado.Persona.Nombre);
        Assert.Equal("29123456", creado.Persona.Dni);
    }

    [Fact]
    public async Task Acepta_UnUsuarioSinPersonaAsociada()
    {
        // Es un caso válido y habitual, no una excepción (FR-008).
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/usuarios",
            AltaValida("sin.persona", personaId: null));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    [Fact]
    public async Task NoDevuelve_LaPassword_NiSuHash_EnNingunCampo()
    {
        // FR-004 y SC-004: la contraseña no sale hacia ninguna respuesta.
        // El username no contiene la palabra "password" a propósito: si la contuviera, la aserción
        // daría un falso positivo sobre su propio valor.
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsJsonAsync("/api/usuarios", AltaValida("sin.clave"));

        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.DoesNotContain("password", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(DatosDePrueba.PasswordValida, cuerpo, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rechaza_ElAlta_ParaUnUsuarioSinElPermisoDeGestion()
    {
        // FR-007: el módulo es sólo para el rol Administrador del sistema.
        var cliente = await app.CrearClienteComoAsync("trafico.cualquiera", CodigosRol.Trafico);

        var respuesta = await cliente.PostAsJsonAsync("/api/usuarios", AltaValida("no.deberia"));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    private static async Task AssertErrorAsync(
        HttpResponseMessage respuesta,
        HttpStatusCode estadoEsperado,
        string codigoEsperado)
    {
        Assert.Equal(estadoEsperado, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();

        Assert.NotNull(error);
        Assert.Equal(codigoEsperado, error.Codigo);
        Assert.False(string.IsNullOrWhiteSpace(error.Mensaje));
    }

    private record RespuestaUsuario(
        int Id,
        string Username,
        string Email,
        string Estado,
        IReadOnlyList<RolLeido> Roles,
        DateTime FechaAlta,
        DateTime? UltimoAcceso);

    private record RespuestaUsuarioConPersona(int Id, string Username, PersonaLeida? Persona);

    private record PersonaLeida(int Id, string Nombre, string Apellido, string Dni);

    private record RolLeido(string Codigo, string Nombre);

    private record RespuestaError(string Codigo, string Mensaje, string? Campo);
}
