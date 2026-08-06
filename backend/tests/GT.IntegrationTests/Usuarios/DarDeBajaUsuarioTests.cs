using System.Net;
using System.Net.Http.Json;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Usuarios;

/// <summary>
/// User Story 5: baja de usuarios.
///
/// Cubre FR-006 (la baja es lógica: el registro no se borra y sigue visible), FR-019 y SC-005 (no
/// dejar al sistema sin administradores) y SC-006 (el usuario dado de baja ya no puede autenticarse).
/// </summary>
public class DarDeBajaUsuarioTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task DejaElUsuarioInactivo_SinBorrarSuRegistro()
    {
        // FR-006: no hay borrado físico en ninguna circunstancia.
        var cliente = await app.CrearClienteAutenticadoAsync();
        var usuario = await app.CrearUsuarioAsync("baja.logica");

        var respuesta = await cliente.DeleteAsync($"/api/usuarios/{usuario.Id}");

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);

        var recargado = await app.RecargarUsuarioAsync(usuario.Id);

        Assert.Equal(EstadoUsuario.Inactivo, recargado.Estado);
    }

    [Fact]
    public async Task ElUsuarioDadoDeBaja_SigueApareciendoEnElListado()
    {
        // FR-006: sigue visible con su estado, no desaparece.
        var cliente = await app.CrearClienteAutenticadoAsync();
        var usuario = await app.CrearUsuarioAsync("baja.visible");

        await cliente.DeleteAsync($"/api/usuarios/{usuario.Id}");

        var usuarios = await cliente.GetFromJsonAsync<List<UsuarioLeido>>("/api/usuarios");

        var enElListado = Assert.Single(usuarios!.Where(u => u.Username == "baja.visible"));
        Assert.Equal("inactivo", enElListado.Estado);
    }

    [Fact]
    public async Task ElUsuarioDadoDeBaja_YaNoPuedeIniciarSesion()
    {
        // SC-006.
        var cliente = await app.CrearClienteAutenticadoAsync();
        var usuario = await app.CrearUsuarioAsync("baja.sin.ingreso");

        await cliente.DeleteAsync($"/api/usuarios/{usuario.Id}");

        var intento = app.CrearCliente();

        var ingreso = await intento.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "baja.sin.ingreso", password = DatosDePrueba.PasswordValida });

        Assert.Equal(HttpStatusCode.Forbidden, ingreso.StatusCode);
    }

    [Fact]
    public async Task Corta_LaSesionAbierta_DelUsuarioDadoDeBaja()
    {
        // SC-006: "a más tardar en su siguiente intento de uso del sistema".
        var administrador = await app.CrearClienteAutenticadoAsync();
        var usuario = await app.CrearUsuarioAsync("baja.con.sesion");

        var suSesion = app.CrearCliente();

        await suSesion.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "baja.con.sesion", password = DatosDePrueba.PasswordValida });

        Assert.Equal(HttpStatusCode.OK, (await suSesion.GetAsync("/api/auth/sesion")).StatusCode);

        await administrador.DeleteAsync($"/api/usuarios/{usuario.Id}");

        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await suSesion.GetAsync("/api/auth/sesion")).StatusCode);
    }

    [Fact]
    public async Task Rechaza_DarDeBajaAlUnicoAdministradorActivo()
    {
        // FR-019 y SC-005, por el tercer camino: la baja.
        var cliente = await app.CrearClienteAutenticadoAsync();
        var administrador = await app.ObtenerAdministradorAsync();

        var respuesta = await cliente.DeleteAsync($"/api/usuarios/{administrador.Id}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("ultimo_administrador", error!.Codigo);

        var recargado = await app.RecargarUsuarioAsync(administrador.Id);
        Assert.Equal(EstadoUsuario.Activo, recargado.Estado);
    }

    [Fact]
    public async Task Permite_DarDeBajaAUnAdministrador_CuandoQuedaOtroActivo()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var segundo = await app.CrearUsuarioAsync("admin.prescindible", CodigosRol.AdministradorSistema);

        var respuesta = await cliente.DeleteAsync($"/api/usuarios/{segundo.Id}");

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }

    [Fact]
    public async Task NoBorra_NingunaFila_DeLaTablaDeUsuarios()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var usuario = await app.CrearUsuarioAsync("cuenta.filas");

        var antes = await app.ConAlcanceAsync(contexto => contexto.Usuarios.CountAsync());

        await cliente.DeleteAsync($"/api/usuarios/{usuario.Id}");

        var despues = await app.ConAlcanceAsync(contexto => contexto.Usuarios.CountAsync());

        Assert.Equal(antes, despues);
    }

    [Fact]
    public async Task Devuelve_NoEncontrado_CuandoElUsuarioNoExiste()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.DeleteAsync("/api/usuarios/999999");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    private record UsuarioLeido(int Id, string Username, string Estado);

    private record RespuestaError(string Codigo, string Mensaje, string? Campo);
}
