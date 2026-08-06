using System.Net.Http.Json;
using GT.Application.Usuarios;
using GT.Domain.Personas;
using GT.Domain.Usuarios;
using GT.Infrastructure.DatosIniciales;
using GT.Infrastructure.Persistencia;
using GT.Infrastructure.Seguridad;
using GT.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GT.IntegrationTests.Usuarios;

/// <summary>Enviador que nunca entrega, para ejercitar el camino de fallo de FR-021.</summary>
public class EnviadorQueSiempreFalla : IEnviadorCorreo
{
    public Task<bool> EnviarAsync(
        string destinatario,
        string asunto,
        string cuerpo,
        CancellationToken cancelacion = default) => Task.FromResult(false);
}

/// <summary>
/// Ayudas para armar el escenario de los tests del Módulo 2.
///
/// Insertan directo en la base a propósito: sirven para <b>preparar</b> el estado previo, no para
/// verificar el alta. Lo que se está probando en cada test siempre pasa por la API.
/// </summary>
public static class DatosDePrueba
{
    public const string PasswordValida = "Password.1234";

    /// <summary>Crea un usuario activo con el rol indicado, saltando la API.</summary>
    public static Task<Usuario> CrearUsuarioAsync(
        this AplicacionDePrueba app,
        string username,
        string rol = CodigosRol.Gerencia,
        EstadoUsuario estado = EstadoUsuario.Activo,
        string password = PasswordValida,
        int? personaId = null) =>
        app.ConAlcanceAsync(async contexto =>
        {
            var hasheador = new HasheadorPassword();
            var rolAsignado = await contexto.Roles.FirstAsync(r => r.Codigo == rol);
            var ahora = DateTime.UtcNow;

            var usuario = new Usuario
            {
                Username = username,
                UsernameNormalizado = username.ToUpperInvariant(),
                Email = $"{username}@gt.com.ar",
                EmailNormalizado = $"{username}@gt.com.ar".ToLowerInvariant(),
                PasswordHash = hasheador.Hashear(password),
                Estado = estado,
                FechaAlta = ahora,
                PasswordActualizadaEn = ahora,
                PersonaId = personaId,
            };

            usuario.Roles.Add(rolAsignado);
            contexto.Usuarios.Add(usuario);
            await contexto.SaveChangesAsync();

            return usuario;
        });

    /// <summary>Registra una persona en el padrón, saltando la API.</summary>
    public static Task<Persona> CrearPersonaAsync(
        this AplicacionDePrueba app,
        string dni,
        string nombre = "Juan",
        string apellido = "Pérez",
        TipoIntegrante tipo = TipoIntegrante.Chofer,
        bool activa = true) =>
        app.ConAlcanceAsync(async contexto =>
        {
            var persona = new Persona
            {
                Nombre = nombre,
                Apellido = apellido,
                Dni = dni,
                Tipo = tipo,
                Telefono = "11-5555-5555",
                Email = $"{dni}@gt.com.ar",
                FechaNacimiento = new DateOnly(1990, 5, 17),
                Activa = activa,
            };

            contexto.Personas.Add(persona);
            await contexto.SaveChangesAsync();

            return persona;
        });

    /// <summary>
    /// Fija una contraseña conocida <b>conservando</b> la marca de contraseña temporal.
    ///
    /// Hace falta porque los tests no leen el correo, así que no pueden conocer la temporal que
    /// generó un restablecimiento. Deja al usuario en el mismo estado en el que quedaría después de
    /// entrar con ella: contraseña que sirve, y marca de temporal todavía puesta.
    /// </summary>
    public static Task FijarPasswordConservandoTemporalAsync(
        this AplicacionDePrueba app,
        int idUsuario,
        string password) =>
        app.EnLaBaseAsync(async contexto =>
        {
            var hasheador = new HasheadorPassword();
            var usuario = await contexto.Usuarios.FirstAsync(u => u.Id == idUsuario);

            usuario.PasswordHash = hasheador.Hashear(password);

            await contexto.SaveChangesAsync();
        });

    /// <summary>
    /// Una vista de la aplicación con el envío de correo siempre fallando, para poder verificar el
    /// camino de FR-021 sin depender de un servidor SMTP ni de tiempos de conexión agotados.
    ///
    /// Comparte la misma base que la fixture original, así que los datos preparados antes siguen
    /// estando.
    /// </summary>
    public static WebApplicationFactory<Program> ConEnviadorQueFalla(this AplicacionDePrueba app) =>
        app.WithWebHostBuilder(constructor =>
            constructor.ConfigureTestServices(servicios =>
                servicios.AddScoped<IEnviadorCorreo, EnviadorQueSiempreFalla>()));

    /// <summary>
    /// Cliente autenticado sobre una fábrica derivada. La misma mecánica que
    /// <c>AplicacionDePrueba.CrearClienteAutenticadoAsync</c>, disponible para las vistas que
    /// reemplazan algún servicio.
    /// </summary>
    public static async Task<HttpClient> CrearClienteAdministradorAsync(
        this WebApplicationFactory<Program> fabrica)
    {
        var cliente = fabrica.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false,
        });

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                username = SembradorInicial.UsernameAdministrador,
                password = AplicacionDePrueba.PasswordAdministrador,
            });

        respuesta.EnsureSuccessStatusCode();

        return cliente;
    }

    /// <summary>Recarga un usuario desde la base, con sus roles y su persona.</summary>
    public static Task<Usuario> RecargarUsuarioAsync(this AplicacionDePrueba app, int id) =>
        app.ConAlcanceAsync(contexto => contexto.Usuarios
            .Include(usuario => usuario.Roles)
            .Include(usuario => usuario.Persona)
            .AsNoTracking()
            .FirstAsync(usuario => usuario.Id == id));

    public static Task<Persona?> RecargarPersonaAsync(this AplicacionDePrueba app, int id) =>
        app.ConAlcanceAsync(contexto => contexto.Personas
            .AsNoTracking()
            .FirstOrDefaultAsync(persona => persona.Id == id));

    /// <summary>
    /// Cliente autenticado como un usuario recién creado, para verificar la autorización desde una
    /// cuenta que <b>no</b> es el administrador.
    /// </summary>
    public static async Task<HttpClient> CrearClienteComoAsync(
        this AplicacionDePrueba app,
        string username,
        string rol = CodigosRol.Gerencia)
    {
        await app.CrearUsuarioAsync(username, rol);

        var cliente = app.CrearCliente();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password = PasswordValida });

        respuesta.EnsureSuccessStatusCode();

        return cliente;
    }
}
