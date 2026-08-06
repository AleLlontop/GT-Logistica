using System.Net;
using System.Net.Http.Json;
using GT.Domain.Usuarios;
using GT.Infrastructure.DatosIniciales;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Usuarios;

/// <summary>
/// User Story 3: edición de usuarios.
///
/// Cubre FR-015 (conservar el propio username o email no es conflicto), FR-016 y SC-006 (la sesión
/// se corta al desactivar la cuenta) y FR-019 (protección del último administrador).
/// </summary>
public class ModificarUsuarioTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private static object Edicion(string username, string email, string estado = "activo", int? personaId = null) =>
        new { username, email, estado, personaId };

    [Fact]
    public async Task Guarda_UnCambioValido()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var usuario = await app.CrearUsuarioAsync("edita.valido");

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/usuarios/{usuario.Id}",
            Edicion("edita.valido", "nuevo.mail@gt.com.ar"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var recargado = await app.RecargarUsuarioAsync(usuario.Id);

        Assert.Equal("nuevo.mail@gt.com.ar", recargado.Email);
        Assert.Equal("nuevo.mail@gt.com.ar", recargado.EmailNormalizado);
    }

    [Fact]
    public async Task Permite_ConservarElPropioUsernameYEmail_SinTratarloComoConflicto()
    {
        // FR-015: la comparación de unicidad excluye al propio usuario.
        var cliente = await app.CrearClienteAutenticadoAsync();
        var usuario = await app.CrearUsuarioAsync("conserva.datos");

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/usuarios/{usuario.Id}",
            Edicion("conserva.datos", "conserva.datos@gt.com.ar", estado: "inactivo"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Rechaza_UnUsernameQueYaEsDeOtroUsuario()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        await app.CrearUsuarioAsync("ya.existe.este");
        var usuario = await app.CrearUsuarioAsync("quiere.el.otro");

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/usuarios/{usuario.Id}",
            Edicion("ya.existe.este", "quiere.el.otro@gt.com.ar"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("username_duplicado", error!.Codigo);
    }

    [Fact]
    public async Task Corta_LaSesion_CuandoLaCuentaPasaAInactiva()
    {
        // FR-016 y SC-006. No requiere código nuevo del Módulo 2: lo resuelve el RevalidadorSesion
        // del Módulo 1 (research §7). Este test verifica que efectivamente lo cubre.
        var administrador = await app.CrearClienteAutenticadoAsync();
        var usuario = await app.CrearUsuarioAsync("desactiva.sesion");

        var suSesion = app.CrearCliente();

        await suSesion.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "desactiva.sesion", password = DatosDePrueba.PasswordValida });

        Assert.Equal(HttpStatusCode.OK, (await suSesion.GetAsync("/api/auth/sesion")).StatusCode);

        await administrador.PutAsJsonAsync(
            $"/api/usuarios/{usuario.Id}",
            Edicion("desactiva.sesion", "desactiva.sesion@gt.com.ar", estado: "inactivo"));

        var despues = await suSesion.GetAsync("/api/auth/sesion");

        Assert.Equal(HttpStatusCode.Unauthorized, despues.StatusCode);
    }

    [Fact]
    public async Task Rechaza_DesactivarAlUnicoAdministradorActivo()
    {
        // FR-019 y SC-005, incluso siendo la propia cuenta de quien opera.
        var cliente = await app.CrearClienteAutenticadoAsync();
        var administrador = await app.ObtenerAdministradorAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/usuarios/{administrador.Id}",
            Edicion(
                SembradorInicial.UsernameAdministrador,
                SembradorInicial.EmailAdministradorInicial,
                estado: "inactivo"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("ultimo_administrador", error!.Codigo);

        // Y no cambió nada.
        var recargado = await app.RecargarUsuarioAsync(administrador.Id);
        Assert.Equal(EstadoUsuario.Activo, recargado.Estado);
    }

    [Fact]
    public async Task Permite_DesactivarAUnAdministrador_CuandoQuedaOtroActivo()
    {
        // La protección no es un bloqueo ciego: con un segundo administrador, la operación procede.
        var cliente = await app.CrearClienteAutenticadoAsync();
        var segundo = await app.CrearUsuarioAsync("segundo.admin", CodigosRol.AdministradorSistema);

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/usuarios/{segundo.Id}",
            Edicion("segundo.admin", "segundo.admin@gt.com.ar", estado: "inactivo"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Libera_LaPersona_AlDesasociarlaConNull()
    {
        // FR-008: desasociarla explícitamente es la única forma de liberarla.
        var cliente = await app.CrearClienteAutenticadoAsync();
        var persona = await app.CrearPersonaAsync("27222111");
        var usuario = await app.CrearUsuarioAsync("suelta.persona", personaId: persona.Id);

        await cliente.PutAsJsonAsync(
            $"/api/usuarios/{usuario.Id}",
            Edicion("suelta.persona", "suelta.persona@gt.com.ar", personaId: null));

        var recargado = await app.RecargarUsuarioAsync(usuario.Id);
        Assert.Null(recargado.PersonaId);

        // Y ahora otro usuario sí puede tomarla.
        var otro = await app.CrearUsuarioAsync("toma.persona");

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/usuarios/{otro.Id}",
            Edicion("toma.persona", "toma.persona@gt.com.ar", personaId: persona.Id));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Reactiva_UnUsuarioDadoDeBaja_DevolviendoleElAcceso()
    {
        // Caso límite de la spec: la reactivación se hace cambiando el estado a `activo` desde la
        // edición, y el usuario tiene que seguir cumpliendo la regla de tener al menos un rol.
        var cliente = await app.CrearClienteAutenticadoAsync();
        var usuario = await app.CrearUsuarioAsync("vuelve.al.ruedo", estado: EstadoUsuario.Inactivo);

        // Estando inactivo no puede entrar.
        var antes = app.CrearCliente();

        var ingresoAntes = await antes.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "vuelve.al.ruedo", password = DatosDePrueba.PasswordValida });

        Assert.Equal(HttpStatusCode.Forbidden, ingresoAntes.StatusCode);

        var respuesta = await cliente.PutAsJsonAsync(
            $"/api/usuarios/{usuario.Id}",
            Edicion("vuelve.al.ruedo", "vuelve.al.ruedo@gt.com.ar", estado: "activo"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var recargado = await app.RecargarUsuarioAsync(usuario.Id);

        Assert.Equal(EstadoUsuario.Activo, recargado.Estado);
        Assert.NotEmpty(recargado.Roles);

        // Y vuelve a poder ingresar.
        var despues = app.CrearCliente();

        var ingresoDespues = await despues.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "vuelve.al.ruedo", password = DatosDePrueba.PasswordValida });

        Assert.Equal(HttpStatusCode.OK, ingresoDespues.StatusCode);
    }

    [Fact]
    public async Task Devuelve_NoEncontrado_CuandoElUsuarioNoExiste()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PutAsJsonAsync(
            "/api/usuarios/999999",
            Edicion("no.existe", "no.existe@gt.com.ar"));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    private record RespuestaError(string Codigo, string Mensaje, string? Campo);
}
