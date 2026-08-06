using System.Net;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Usuarios;

/// <summary>
/// User Story 3: restablecimiento de contraseña.
///
/// Cubre FR-009 (la contraseña se genera y no se expone), FR-021 (un envío fallido no revierte el
/// restablecimiento), FR-032 y SC-010 (corta las sesiones abiertas de ese usuario).
/// </summary>
public class RestablecerPasswordTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task NoDevuelve_LaContraseñaGenerada_EnNingunCampoDeLaRespuesta()
    {
        // SC-004: el responsable de sistemas confirma el envío, no lee la contraseña.
        var cliente = await app.CrearClienteAutenticadoAsync();
        var usuario = await app.CrearUsuarioAsync("restablece.oculta");

        var respuesta = await cliente.PostAsync(
            $"/api/usuarios/{usuario.Id}/restablecer-password",
            content: null);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var cuerpo = await respuesta.Content.ReadAsStringAsync();

        Assert.DoesNotContain("password", cuerpo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("hash", cuerpo, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Marca_LaContraseñaComoTemporal_ParaQueVenzaALas24Horas()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var usuario = await app.CrearUsuarioAsync("restablece.temporal");

        await cliente.PostAsync($"/api/usuarios/{usuario.Id}/restablecer-password", content: null);

        var recargado = await app.RecargarUsuarioAsync(usuario.Id);

        // La regla de vencimiento ya existe en el Módulo 1; este módulo sólo escribe la marca.
        Assert.NotNull(recargado.PasswordTemporalGeneradaEn);
    }

    [Fact]
    public async Task Cambia_LaContraseña_DeModoQueLaAnteriorDejaDeServir()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var usuario = await app.CrearUsuarioAsync("restablece.invalida");

        await cliente.PostAsync($"/api/usuarios/{usuario.Id}/restablecer-password", content: null);

        var intento = app.CrearCliente();

        var ingreso = await intento.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "restablece.invalida", password = DatosDePrueba.PasswordValida });

        Assert.Equal(HttpStatusCode.Unauthorized, ingreso.StatusCode);
    }

    [Fact]
    public async Task Corta_LaSesionAbiertaDeEseUsuario_EnSuSiguienteOperacion()
    {
        // FR-032 y SC-010: es el corazón de esta historia. Una contraseña que dejó de ser válida no
        // puede seguir sosteniendo una sesión viva.
        var administrador = await app.CrearClienteAutenticadoAsync();
        var usuario = await app.CrearUsuarioAsync("sesion.cortada");

        // El usuario está trabajando en otra máquina.
        var suSesion = app.CrearCliente();

        var ingreso = await suSesion.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "sesion.cortada", password = DatosDePrueba.PasswordValida });

        Assert.Equal(HttpStatusCode.OK, ingreso.StatusCode);

        // Su sesión funciona antes del restablecimiento.
        var antes = await suSesion.GetAsync("/api/auth/sesion");
        Assert.Equal(HttpStatusCode.OK, antes.StatusCode);

        // El responsable de sistemas le restablece la contraseña.
        await administrador.PostAsync(
            $"/api/usuarios/{usuario.Id}/restablecer-password",
            content: null);

        // En su próxima acción, esa sesión ya no vale.
        var despues = await suSesion.GetAsync("/api/auth/sesion");

        Assert.Equal(HttpStatusCode.Unauthorized, despues.StatusCode);
    }

    [Fact]
    public async Task NoCorta_LasSesionesDeOtrosUsuarios()
    {
        // El corte es de la persona a la que se le restableció, no del sistema entero.
        var administrador = await app.CrearClienteAutenticadoAsync();
        var afectado = await app.CrearUsuarioAsync("corte.afectado");
        await app.CrearUsuarioAsync("corte.ajeno");

        var sesionAjena = app.CrearCliente();

        await sesionAjena.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "corte.ajeno", password = DatosDePrueba.PasswordValida });

        await administrador.PostAsync(
            $"/api/usuarios/{afectado.Id}/restablecer-password",
            content: null);

        var respuesta = await sesionAjena.GetAsync("/api/auth/sesion");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Informa_ElEnvio_ConUnMensajeListoParaMostrar()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();
        var usuario = await app.CrearUsuarioAsync("restablece.mensaje");

        var respuesta = await cliente.PostAsync(
            $"/api/usuarios/{usuario.Id}/restablecer-password",
            content: null);

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaRestablecimiento>();

        Assert.NotNull(cuerpo);
        // En los tests no hay SMTP configurado, así que se usa el enviador que registra al log y el
        // envío se considera exitoso (research §1).
        Assert.True(cuerpo.Enviado);
        Assert.Contains("24 horas", cuerpo.Mensaje);
        Assert.Contains("sesión abierta, se cerró", cuerpo.Mensaje);
    }

    [Fact]
    public async Task CuandoElEnvioFalla_InformaElFallo_PeroNoRevierteElRestablecimiento()
    {
        // FR-021: es la mitad del requisito que el camino feliz no toca. Un correo que no sale no
        // puede deshacer una contraseña que ya se cambió: el usuario quedaría con una que quizá ya
        // no recuerda y sin forma de avisar.
        var usuario = await app.CrearUsuarioAsync("envio.fallido");

        // Se reemplaza el enviador por uno que siempre falla, en vez de apuntar a un SMTP inexistente:
        // así el test no depende de la red ni espera un tiempo de conexión agotado.
        await using var conCorreoRoto = app.ConEnviadorQueFalla();
        var cliente = await conCorreoRoto.CrearClienteAdministradorAsync();

        var respuesta = await cliente.PostAsync(
            $"/api/usuarios/{usuario.Id}/restablecer-password",
            content: null);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<RespuestaRestablecimiento>();

        Assert.NotNull(cuerpo);
        Assert.False(cuerpo.Enviado);
        Assert.Contains("no pudimos enviar el correo", cuerpo.Mensaje);

        // Y el restablecimiento quedó hecho igual: la contraseña anterior ya no sirve.
        var recargado = await app.RecargarUsuarioAsync(usuario.Id);
        Assert.NotNull(recargado.PasswordTemporalGeneradaEn);

        var intento = app.CrearCliente();

        var ingreso = await intento.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "envio.fallido", password = DatosDePrueba.PasswordValida });

        Assert.Equal(HttpStatusCode.Unauthorized, ingreso.StatusCode);
    }

    [Fact]
    public async Task Devuelve_NoEncontrado_CuandoElUsuarioNoExiste()
    {
        var cliente = await app.CrearClienteAutenticadoAsync();

        var respuesta = await cliente.PostAsync(
            "/api/usuarios/999999/restablecer-password",
            content: null);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    private record RespuestaRestablecimiento(bool Enviado, string Mensaje);
}
