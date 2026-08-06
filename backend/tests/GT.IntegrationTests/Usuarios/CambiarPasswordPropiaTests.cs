using System.Net;
using System.Net.Http.Json;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Infraestructura;

namespace GT.IntegrationTests.Usuarios;

/// <summary>
/// User Story 7: cambio de contraseña propia.
///
/// Cubre FR-029 (cualquier usuario autenticado, sin importar sus roles), FR-030 (exige la actual
/// correcta y una nueva de 8 o más), FR-031 (la nueva queda definitiva) y FR-032 (la sesión que hizo
/// el cambio sobrevive; las demás del mismo usuario se cortan).
///
/// <b>Es el único endpoint del módulo sin política de permiso</b>. Los dos primeros tests son los
/// que verifican esa excepción, y son la razón por la que esta historia se implementa aparte.
/// </summary>
public class CambiarPasswordPropiaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private const string PasswordNueva = "MiPasswordNueva.99"; // NOSONAR: valor de prueba

    private static object Cambio(string actual, string nueva) =>
        new { passwordActual = actual, passwordNueva = nueva };

    [Fact]
    public async Task Permite_ElCambio_AUnUsuarioSinElPermisoDeGestionDeUsuarios()
    {
        // FR-029: es la excepción a FR-007. Alguien de Tráfico no puede entrar al módulo, pero sí
        // tiene que poder cambiar su propia contraseña.
        var cliente = await app.CrearClienteComoAsync("trafico.cambia", CodigosRol.Trafico);

        // No tiene acceso al resto del módulo…
        Assert.Equal(HttpStatusCode.Forbidden, (await cliente.GetAsync("/api/usuarios")).StatusCode);

        // …pero sí a esto.
        var respuesta = await cliente.PostAsJsonAsync(
            "/api/mi-cuenta/contrasena",
            Cambio(DatosDePrueba.PasswordValida, PasswordNueva));

        Assert.Equal(HttpStatusCode.NoContent, respuesta.StatusCode);
    }

    [Fact]
    public async Task Rechaza_ElCambio_SinSesionIniciada()
    {
        // Sigue exigiendo estar autenticado: la excepción es del permiso, no de la sesión.
        var cliente = app.CrearCliente();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/mi-cuenta/contrasena",
            Cambio(DatosDePrueba.PasswordValida, PasswordNueva));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Cambia_LaContraseña_DeModoQueSirveLaNuevaYNoLaAnterior()
    {
        var cliente = await app.CrearClienteComoAsync("cambia.propia", CodigosRol.Gerencia);

        await cliente.PostAsJsonAsync(
            "/api/mi-cuenta/contrasena",
            Cambio(DatosDePrueba.PasswordValida, PasswordNueva));

        var conLaNueva = app.CrearCliente();

        var ingresoNuevo = await conLaNueva.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "cambia.propia", password = PasswordNueva });

        Assert.Equal(HttpStatusCode.OK, ingresoNuevo.StatusCode);

        var conLaVieja = app.CrearCliente();

        var ingresoViejo = await conLaVieja.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "cambia.propia", password = DatosDePrueba.PasswordValida });

        Assert.Equal(HttpStatusCode.Unauthorized, ingresoViejo.StatusCode);
    }

    [Fact]
    public async Task Rechaza_ElCambio_CuandoLaContraseñaActualEsIncorrecta()
    {
        var cliente = await app.CrearClienteComoAsync("actual.incorrecta", CodigosRol.Gerencia);

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/mi-cuenta/contrasena",
            Cambio("EstaNoEsLaSuya.1", PasswordNueva));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("password_actual_incorrecta", error!.Codigo);

        // Y no cambió nada: la original sigue sirviendo.
        var otro = app.CrearCliente();

        var ingreso = await otro.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "actual.incorrecta", password = DatosDePrueba.PasswordValida });

        Assert.Equal(HttpStatusCode.OK, ingreso.StatusCode);
    }

    [Fact]
    public async Task Rechaza_UnaContraseñaNuevaDeMenosDeOchoCaracteres()
    {
        var cliente = await app.CrearClienteComoAsync("nueva.corta", CodigosRol.Gerencia);

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/mi-cuenta/contrasena",
            Cambio(DatosDePrueba.PasswordValida, "1234567"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var error = await respuesta.Content.ReadFromJsonAsync<RespuestaError>();
        Assert.Equal("datos_invalidos", error!.Codigo);
    }

    [Fact]
    public async Task Deja_LaContraseñaComoDefinitiva_CuandoLaAnteriorEraTemporal()
    {
        // FR-031: es lo que cierra el circuito del restablecimiento. Sin esto, la contraseña seguiría
        // venciendo a las 24 horas y el usuario quedaría afuera igual.
        var administrador = await app.CrearClienteAutenticadoAsync();
        var usuario = await app.CrearUsuarioAsync("temporal.a.definitiva");

        await administrador.PostAsync(
            $"/api/usuarios/{usuario.Id}/restablecer-password",
            content: null);

        var conTemporal = await app.RecargarUsuarioAsync(usuario.Id);
        Assert.NotNull(conTemporal.PasswordTemporalGeneradaEn);

        // El usuario entra con la temporal. Como los tests no leen el correo, se la fija a mano y se
        // conserva la marca de temporal, que es lo que este test verifica que se limpia.
        await app.FijarPasswordConservandoTemporalAsync(usuario.Id, PasswordNueva);

        var suCliente = app.CrearCliente();

        await suCliente.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "temporal.a.definitiva", password = PasswordNueva });

        await suCliente.PostAsJsonAsync(
            "/api/mi-cuenta/contrasena",
            Cambio(PasswordNueva, "YaEsMiaDefinitiva.7"));

        var recargado = await app.RecargarUsuarioAsync(usuario.Id);

        Assert.Null(recargado.PasswordTemporalGeneradaEn);
    }

    [Fact]
    public async Task Conserva_LaSesionDesdeLaQueSeHizoElCambio()
    {
        // FR-032 y escenario 2 de la User Story 7: la sesión propia no se cae. Es lo que hace que el
        // corte de sesiones no sea un estorbo para quien cambia su contraseña.
        var cliente = await app.CrearClienteComoAsync("conserva.su.sesion", CodigosRol.Gerencia);

        var cambio = await cliente.PostAsJsonAsync(
            "/api/mi-cuenta/contrasena",
            Cambio(DatosDePrueba.PasswordValida, PasswordNueva));

        Assert.Equal(HttpStatusCode.NoContent, cambio.StatusCode);

        var despues = await cliente.GetAsync("/api/auth/sesion");

        Assert.Equal(HttpStatusCode.OK, despues.StatusCode);
    }

    [Fact]
    public async Task Corta_LasOtrasSesionesDelMismoUsuario()
    {
        // FR-032: la sesión que hace el cambio sobrevive, las demás no.
        var primera = await app.CrearClienteComoAsync("dos.sesiones", CodigosRol.Gerencia);

        var segunda = app.CrearCliente();

        await segunda.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "dos.sesiones", password = DatosDePrueba.PasswordValida });

        Assert.Equal(HttpStatusCode.OK, (await segunda.GetAsync("/api/auth/sesion")).StatusCode);

        await primera.PostAsJsonAsync(
            "/api/mi-cuenta/contrasena",
            Cambio(DatosDePrueba.PasswordValida, PasswordNueva));

        // La que hizo el cambio sigue viva…
        Assert.Equal(HttpStatusCode.OK, (await primera.GetAsync("/api/auth/sesion")).StatusCode);

        // …y la otra no.
        Assert.Equal(
            HttpStatusCode.Unauthorized,
            (await segunda.GetAsync("/api/auth/sesion")).StatusCode);
    }

    [Fact]
    public async Task NoPermite_CambiarLaContraseñaDeOtroUsuario()
    {
        // El usuario afectado sale de los claims de la sesión, nunca de la petición: no hay forma de
        // indicar a alguien distinto (research §9). Se verifica que la cuenta ajena queda intacta.
        var otro = await app.CrearUsuarioAsync("victima.potencial");
        var cliente = await app.CrearClienteComoAsync("atacante", CodigosRol.Gerencia);

        await cliente.PostAsJsonAsync(
            "/api/mi-cuenta/contrasena",
            Cambio(DatosDePrueba.PasswordValida, PasswordNueva));

        var suCliente = app.CrearCliente();

        var ingreso = await suCliente.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "victima.potencial", password = DatosDePrueba.PasswordValida });

        Assert.Equal(HttpStatusCode.OK, ingreso.StatusCode);
        Assert.NotEqual(0, otro.Id);
    }

    private record RespuestaError(string Codigo, string Mensaje, string? Campo);
}
