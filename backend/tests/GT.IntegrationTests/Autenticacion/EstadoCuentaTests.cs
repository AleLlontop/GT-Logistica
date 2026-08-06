using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using GT.Domain.Usuarios;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Autenticacion;

/// <summary>
/// Cubre la User Story 4 y la parte de la User Story 3 que no se puede verificar operando la
/// aplicación.
///
/// Hasta que exista el Módulo 2 no hay pantalla para cambiar el estado de una cuenta ni para generar
/// una contraseña temporal, así que estos tests **son** la verificación de esos requisitos. Está
/// anotado como deuda conocida en las Assumptions de la spec.
/// </summary>
public class EstadoCuentaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private const string PasswordCorrecta = "Correcta.1234";

    /// <summary>FR-004: cuenta inactiva con contraseña correcta recibe el mensaje específico.</summary>
    [Fact]
    public async Task RechazaCuentaInactiva()
    {
        var usuario = await CrearUsuarioAsync("inactiva", EstadoUsuario.Inactivo);

        var error = await IntentarIngresarAsync(
            usuario.Username,
            PasswordCorrecta,
            HttpStatusCode.Forbidden);

        Assert.Equal("cuenta_no_habilitada", error.Codigo);
        Assert.Contains("responsable de sistemas", error.Mensaje);
    }

    /// <summary>FR-004: una cuenta bloqueada recibe exactamente el mismo mensaje que una inactiva.</summary>
    [Fact]
    public async Task RechazaCuentaBloqueada()
    {
        var usuario = await CrearUsuarioAsync("bloqueada", EstadoUsuario.Bloqueado);

        var error = await IntentarIngresarAsync(
            usuario.Username,
            PasswordCorrecta,
            HttpStatusCode.Forbidden);

        Assert.Equal("cuenta_no_habilitada", error.Codigo);
    }

    /// <summary>
    /// FR-003 y FR-004: con la contraseña incorrecta sobre una cuenta inactiva, el mensaje es el
    /// genérico y no el de cuenta no habilitada.
    ///
    /// Este test fija el orden de los controles: si el estado se revisara antes que la contraseña,
    /// cualquiera podría descubrir qué cuentas existen probando una contraseña cualquiera.
    /// </summary>
    [Fact]
    public async Task CuentaInactivaConPasswordIncorrecta_DevuelveMensajeGenerico()
    {
        var usuario = await CrearUsuarioAsync("inactiva_mal", EstadoUsuario.Inactivo);

        var error = await IntentarIngresarAsync(
            usuario.Username,
            "Equivocada.9999",
            HttpStatusCode.Unauthorized);

        Assert.Equal("credenciales_invalidas", error.Codigo);
    }

    /// <summary>
    /// FR-017: una contraseña temporal de más de 24 horas se rechaza con el mensaje genérico.
    ///
    /// La escribe el Módulo 2 al restablecer una contraseña; acá se simula poniendo la marca de
    /// tiempo directamente, porque todavía no existe la pantalla que la genera.
    /// </summary>
    [Fact]
    public async Task RechazaPasswordTemporalVencida()
    {
        var usuario = await CrearUsuarioAsync("temporal", EstadoUsuario.Activo);

        await app.EnLaBaseAsync(async contexto =>
        {
            await contexto.Usuarios
                .Where(u => u.Id == usuario.Id)
                .ExecuteUpdateAsync(cambio => cambio.SetProperty(
                    u => u.PasswordTemporalGeneradaEn,
                    DateTime.UtcNow.AddHours(-25)));
        });

        var error = await IntentarIngresarAsync(
            usuario.Username,
            PasswordCorrecta,
            HttpStatusCode.Unauthorized);

        Assert.Equal("credenciales_invalidas", error.Codigo);
    }

    /// <summary>FR-017: dentro de las 24 horas, la contraseña temporal permite entrar sin exigir cambiarla.</summary>
    [Fact]
    public async Task AceptaPasswordTemporalVigente()
    {
        var usuario = await CrearUsuarioAsync("temporal_ok", EstadoUsuario.Activo);

        await app.EnLaBaseAsync(async contexto =>
        {
            await contexto.Usuarios
                .Where(u => u.Id == usuario.Id)
                .ExecuteUpdateAsync(cambio => cambio.SetProperty(
                    u => u.PasswordTemporalGeneradaEn,
                    DateTime.UtcNow.AddHours(-2)));
        });

        var cliente = app.CrearCliente();

        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new
        {
            username = usuario.Username,
            password = PasswordCorrecta,
        });

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    /// <summary>
    /// FR-003 y research §3: un username inexistente y una contraseña incorrecta devuelven cuerpos
    /// idénticos y tardan lo mismo.
    ///
    /// Sin la verificación contra un hash ficticio, el caso "usuario inexistente" respondería en
    /// milisegundos y el otro tardaría lo que tarda el hasheo: esa diferencia delataría qué cuentas
    /// existen y anularía el propósito del mensaje genérico.
    /// </summary>
    [Fact]
    public async Task UsuarioInexistenteYPasswordIncorrecta_SonIndistinguibles()
    {
        var usuario = await CrearUsuarioAsync("existente", EstadoUsuario.Activo);
        var cliente = app.CrearCliente();

        // Una primera llamada descartada, para no medir el costo de arranque en frío.
        await IngresarAsync(cliente, usuario.Username, "Calentando.0000");

        var (errorInexistente, tiempoInexistente) =
            await MedirAsync(cliente, "no_existe_nadie_asi", "Cualquiera.1234");

        var (errorPasswordMala, tiempoPasswordMala) =
            await MedirAsync(cliente, usuario.Username, "Equivocada.9999");

        // Mismo cuerpo, palabra por palabra.
        Assert.Equal(errorInexistente.Codigo, errorPasswordMala.Codigo);
        Assert.Equal(errorInexistente.Mensaje, errorPasswordMala.Mensaje);

        // Y el mismo orden de magnitud: ninguno responde "de inmediato" frente al otro.
        var mayor = Math.Max(tiempoInexistente, tiempoPasswordMala);
        var menor = Math.Min(tiempoInexistente, tiempoPasswordMala);

        Assert.True(
            mayor < menor * 5 + 50,
            $"Los tiempos difieren demasiado: {tiempoInexistente} ms contra {tiempoPasswordMala} ms. " +
            "Esa diferencia permite descubrir qué cuentas existen.");
    }

    private async Task<(ErrorDeRespuesta Error, long Milisegundos)> MedirAsync(
        HttpClient cliente,
        string username,
        string password)
    {
        var cronometro = Stopwatch.StartNew();
        var respuesta = await IngresarAsync(cliente, username, password);
        cronometro.Stop();

        var error = await respuesta.Content.ReadFromJsonAsync<ErrorDeRespuesta>();

        return (error!, cronometro.ElapsedMilliseconds);
    }

    private static Task<HttpResponseMessage> IngresarAsync(
        HttpClient cliente,
        string username,
        string password) =>
        cliente.PostAsJsonAsync("/api/auth/login", new { username, password });

    private async Task<ErrorDeRespuesta> IntentarIngresarAsync(
        string username,
        string password,
        HttpStatusCode esperado)
    {
        var cliente = app.CrearCliente();
        var respuesta = await IngresarAsync(cliente, username, password);

        Assert.Equal(esperado, respuesta.StatusCode);

        return (await respuesta.Content.ReadFromJsonAsync<ErrorDeRespuesta>())!;
    }

    private Task<Usuario> CrearUsuarioAsync(string username, EstadoUsuario estado) =>
        app.ConAlcanceAsync(async contexto =>
        {
            var hasheador = new GT.Infrastructure.Seguridad.HasheadorPassword();
            var rol = await contexto.Roles.FirstAsync(r => r.Codigo == CodigosRol.Administracion);

            var usuario = new Usuario
            {
                Username = username,
                UsernameNormalizado = username.ToUpperInvariant(),
                Email = $"{username}@gt.local",
                EmailNormalizado = $"{username}@gt.local".ToLowerInvariant(),
                PasswordHash = hasheador.Hashear(PasswordCorrecta),
                Estado = estado,
                FechaAlta = DateTime.UtcNow,
                PasswordActualizadaEn = DateTime.UtcNow,
            };

            usuario.Roles.Add(rol);
            contexto.Usuarios.Add(usuario);
            await contexto.SaveChangesAsync();

            return usuario;
        });
}
