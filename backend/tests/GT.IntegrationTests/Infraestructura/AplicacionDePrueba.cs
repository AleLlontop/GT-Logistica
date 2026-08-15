using System.Net.Http.Json;
using GT.Domain.Usuarios;
using GT.Infrastructure.DatosIniciales;
using GT.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace GT.IntegrationTests.Infraestructura;

/// <summary>
/// Levanta la aplicación real contra el SQL Server del compose, con una base de datos propia por
/// corrida para que los tests no se pisen entre sí ni ensucien la base de desarrollo.
///
/// Se usa la aplicación completa, sin reemplazar la persistencia por una en memoria, porque lo que
/// hay que verificar —índices únicos, revalidación por petición, cookies— sólo se comporta de
/// verdad contra SQL Server (research §10).
/// </summary>
public class AplicacionDePrueba : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string PasswordAdministrador = "Administrador.1234";

    private readonly string _nombreBase = $"GtLogistica_Test_{Guid.NewGuid():N}";

    /// <summary>
    /// Volumen de adjuntos propio de la corrida, en el directorio temporal del sistema. Fuera del
    /// repositorio a propósito: los escaneos no se versionan (FR-024, research §3).
    /// </summary>
    private readonly string _rutaDeArchivos =
        Path.Combine(Path.GetTempPath(), $"GtLogistica_Archivos_{Guid.NewGuid():N}");

    /// <summary>
    /// El volumen de la corrida, para los tests que necesitan verificar <b>qué se escribió y qué no</b>.
    ///
    /// Lo usa <c>VistaPreviaTests</c>: FR-033 exige que previsualizar no guarde ningún archivo, y un
    /// archivo huérfano es invisible desde la aplicación, así que la única forma de comprobarlo es mirar
    /// el directorio. El almacén no expone un listado porque a la aplicación no le hace falta.
    /// </summary>
    public string RutaDeArchivos => _rutaDeArchivos;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development evita la redirección forzada a HTTPS y HSTS, que no aportan nada al servidor
        // de pruebas en memoria. La cookie sigue siendo Secure, y por eso los clientes usan una
        // dirección https (ver CrearCliente).
        builder.UseEnvironment(Environments.Development);

        builder.UseSetting("ConnectionStrings:Gt", ConfiguracionEntorno.CadenaDeConexion(_nombreBase));
        builder.UseSetting(SembradorInicial.VariablePasswordInicial, PasswordAdministrador);
        builder.UseSetting("GT_ARCHIVOS_RUTA", _rutaDeArchivos);
    }

    /// <summary>
    /// Cliente que conserva las cookies entre peticiones, como haría un navegador.
    ///
    /// La dirección base es https porque la cookie de sesión es `Secure` (FR-023) y el manejador de
    /// cookies de .NET no la enviaría por http. El servidor de pruebas no usa TLS real, pero
    /// respeta el esquema de la petición, así que la condición se verifica igual.
    /// </summary>
    public HttpClient CrearCliente() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true,
        AllowAutoRedirect = false,
    });

    public async Task<HttpClient> CrearClienteAutenticadoAsync(
        string username = SembradorInicial.UsernameAdministrador,
        string password = PasswordAdministrador)
    {
        var cliente = CrearCliente();

        var respuesta = await cliente.PostAsJsonAsync(
            "/api/auth/login",
            new { username, password });

        respuesta.EnsureSuccessStatusCode();

        return cliente;
    }

    /// <summary>Ejecuta una operación sobre la base, por fuera de la aplicación.</summary>
    public async Task EnLaBaseAsync(Func<GtDbContext, Task> operacion)
    {
        using var alcance = Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<GtDbContext>();

        await operacion(contexto);
    }

    public Task<Usuario> ObtenerAdministradorAsync() =>
        ConAlcanceAsync(contexto => contexto.Usuarios
            .Include(usuario => usuario.Roles)
            .AsNoTracking()
            .FirstAsync(usuario =>
                usuario.UsernameNormalizado == SembradorInicial.UsernameAdministrador.ToUpperInvariant()));

    public async Task<T> ConAlcanceAsync<T>(Func<GtDbContext, Task<T>> operacion)
    {
        using var alcance = Services.CreateScope();
        var contexto = alcance.ServiceProvider.GetRequiredService<GtDbContext>();

        return await operacion(contexto);
    }

    /// <summary>Crea la base y aplica migraciones y datos iniciales levantando la aplicación.</summary>
    public Task InitializeAsync()
    {
        // Basta con pedir un cliente: al construir el host se ejecutan las migraciones y la siembra.
        _ = CreateClient();

        return Task.CompletedTask;
    }

    public new async Task DisposeAsync()
    {
        await BorrarBaseAsync();
        BorrarArchivos();
        await base.DisposeAsync();
    }

    private void BorrarArchivos()
    {
        try
        {
            if (Directory.Exists(_rutaDeArchivos))
            {
                Directory.Delete(_rutaDeArchivos, recursive: true);
            }
        }
        catch
        {
            // Si la limpieza falla queda un directorio temporal huérfano. Es molesto, pero no debe
            // hacer fallar la corrida ni tapar el resultado real de los tests.
        }
    }

    private async Task BorrarBaseAsync()
    {
        try
        {
            var opciones = new DbContextOptionsBuilder<GtDbContext>()
                .UseSqlServer(ConfiguracionEntorno.CadenaDeConexionMaestra())
                .Options;

            await using var maestra = new GtDbContext(opciones);

            // El nombre de una base no se puede pasar como parámetro, así que va concatenado. Es
            // seguro porque lo genera esta misma clase a partir de un Guid, pero se verifica igual
            // antes de armar la sentencia en lugar de confiar en que siga siendo así.
            if (!_nombreBase.All(char.IsLetterOrDigit) && !_nombreBase.Contains('_'))
            {
                return;
            }

            // La base puede quedar con conexiones abiertas del pool; SINGLE_USER las corta.
#pragma warning disable EF1003 // Nombre de base validado arriba; no admite parametrización.
            await maestra.Database.ExecuteSqlRawAsync(
                $"ALTER DATABASE [{_nombreBase}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; " +
                $"DROP DATABASE [{_nombreBase}];");
#pragma warning restore EF1003
        }
        catch
        {
            // Si la limpieza falla, queda una base de prueba huérfana. Es molesto, pero no debe
            // hacer fallar la corrida ni tapar el resultado real de los tests.
        }
    }
}
