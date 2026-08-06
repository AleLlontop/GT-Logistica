using System.Net;
using System.Net.Http.Json;
using GT.Application.Autenticacion;
using GT.Domain.Usuarios;
using GT.Infrastructure.DatosIniciales;
using GT.IntegrationTests.Infraestructura;
using Microsoft.EntityFrameworkCore;

namespace GT.IntegrationTests.Autenticacion;

/// <summary>
/// Cubre FR-021 y SC-007 contra la aplicación real.
///
/// Los escenarios que dependen del paso del tiempo —que la restricción se levante sola al minuto—
/// se verifican en <c>GT.UnitTests</c> con un reloj controlado, para no esperar minutos reales acá.
/// </summary>
public class IntentosFallidosTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    private const string PasswordCorrecta = "Legitima.1234";

    /// <summary>
    /// El escenario que motivó el cambio de FR-021: en la oficina de G&amp;T todos salen por la
    /// misma conexión a internet. Si el contador fuera sólo por origen, el error de tipeo de una
    /// persona dejaría afuera a todas las demás durante un minuto.
    /// </summary>
    [Fact]
    public async Task NoAfectaAOtrasCuentasDelMismoOrigen()
    {
        var distraida = await CrearUsuarioAsync("distraida");
        var puntual = await CrearUsuarioAsync("puntual");

        var cliente = app.CrearCliente();

        // Una persona se equivoca cinco veces seguidas y queda frenada.
        for (var intento = 0; intento < LimiteIntentos.FallosPermitidos; intento++)
        {
            var fallo = await IngresarAsync(cliente, distraida.Username, "Equivocada.9999");
            Assert.Equal(HttpStatusCode.Unauthorized, fallo.StatusCode);
        }

        var frenada = await IngresarAsync(cliente, distraida.Username, PasswordCorrecta);

        Assert.Equal(HttpStatusCode.TooManyRequests, frenada.StatusCode);
        Assert.NotNull(frenada.Headers.RetryAfter);

        var error = await frenada.Content.ReadFromJsonAsync<ErrorDeRespuesta>();
        Assert.Equal("demasiados_intentos", error!.Codigo);

        // Y su compañera, desde el mismo equipo y la misma conexión, entra sin ninguna demora.
        var otra = await IngresarAsync(cliente, puntual.Username, PasswordCorrecta);

        Assert.Equal(HttpStatusCode.OK, otra.StatusCode);
    }

    /// <summary>
    /// FR-016: el freno no cambia el estado de ninguna cuenta. Nadie tiene que destrabar nada, y el
    /// bloqueo de cuentas sigue siendo una acción manual del responsable de sistemas.
    /// </summary>
    [Fact]
    public async Task NingunaCuentaCambiaDeEstadoPorLosIntentos()
    {
        var usuario = await CrearUsuarioAsync("insistente");
        var cliente = app.CrearCliente();

        for (var intento = 0; intento < LimiteIntentos.FallosPermitidos + 2; intento++)
        {
            await IngresarAsync(cliente, usuario.Username, "Equivocada.9999");
        }

        var estado = await app.ConAlcanceAsync(contexto => contexto.Usuarios
            .Where(u => u.Id == usuario.Id)
            .Select(u => u.Estado)
            .FirstAsync());

        Assert.Equal(EstadoUsuario.Activo, estado);
    }

    /// <summary>
    /// FR-021: el freno es por cuenta, no por sesión de navegador. Cambiar de navegador no lo
    /// esquiva mientras el origen siga siendo el mismo.
    /// </summary>
    [Fact]
    public async Task NoSeEsquivaCambiandoDeNavegador()
    {
        var usuario = await CrearUsuarioAsync("porfiada");
        var primerNavegador = app.CrearCliente();

        for (var intento = 0; intento < LimiteIntentos.FallosPermitidos; intento++)
        {
            await IngresarAsync(primerNavegador, usuario.Username, "Equivocada.9999");
        }

        var otroNavegador = app.CrearCliente();
        var respuesta = await IngresarAsync(otroNavegador, usuario.Username, PasswordCorrecta);

        Assert.Equal(HttpStatusCode.TooManyRequests, respuesta.StatusCode);
    }

    /// <summary>FR-021: la cuenta del administrador no tiene ningún trato especial.</summary>
    [Fact]
    public async Task TambienAplicaAlAdministrador()
    {
        var cliente = app.CrearCliente();

        for (var intento = 0; intento < LimiteIntentos.FallosPermitidos; intento++)
        {
            await IngresarAsync(
                cliente,
                SembradorInicial.UsernameAdministrador,
                "Equivocada.9999");
        }

        var respuesta = await IngresarAsync(
            cliente,
            SembradorInicial.UsernameAdministrador,
            AplicacionDePrueba.PasswordAdministrador);

        Assert.Equal(HttpStatusCode.TooManyRequests, respuesta.StatusCode);
    }

    private static Task<HttpResponseMessage> IngresarAsync(
        HttpClient cliente,
        string username,
        string password) =>
        cliente.PostAsJsonAsync("/api/auth/login", new { username, password });

    private Task<Usuario> CrearUsuarioAsync(string username) =>
        app.ConAlcanceAsync(async contexto =>
        {
            var hasheador = new GT.Infrastructure.Seguridad.HasheadorPassword();
            var rol = await contexto.Roles.FirstAsync(r => r.Codigo == CodigosRol.Trafico);

            var usuario = new Usuario
            {
                Username = username,
                UsernameNormalizado = username.ToUpperInvariant(),
                Email = $"{username}@gt.local",
                EmailNormalizado = $"{username}@gt.local".ToLowerInvariant(),
                PasswordHash = hasheador.Hashear(PasswordCorrecta),
                Estado = EstadoUsuario.Activo,
                FechaAlta = DateTime.UtcNow,
                PasswordActualizadaEn = DateTime.UtcNow,
            };

            usuario.Roles.Add(rol);
            contexto.Usuarios.Add(usuario);
            await contexto.SaveChangesAsync();

            return usuario;
        });
}
