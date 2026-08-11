using System.Collections.Concurrent;
using System.Net.Http.Json;
using GT.IntegrationTests.Infraestructura;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GT.IntegrationTests.Viajes;

/// <summary>
/// La consulta del listado <b>se traduce entera a SQL</b>, con los filtros, la búsqueda y la
/// subconsulta de <c>demorado</c> combinados (FR-039, FR-043, convención [003]).
///
/// <b>Por qué hace falta un test así.</b> EF Core sólo traduce lo que ve en el árbol de expresión;
/// una llamada a un método propio rompe la traducción y la consulta pasa a evaluarse en memoria,
/// trayendo la tabla entera. Eso no falla: anda igual, sólo que trae todo. El precio se paga con el
/// volumen real, cuando ya nadie relaciona la lentitud con este cambio.
///
/// El test mira el SQL que EF generó y verifica tres cosas: que la subconsulta al historial viajó
/// como subconsulta correlacionada, que la paginación se resolvió en la base y que la búsqueda usó la
/// colación explícita.
/// </summary>
public class TraduccionConsultaTests(AplicacionDePrueba app) : IClassFixture<AplicacionDePrueba>
{
    [Fact]
    public async Task La_ConsultaDelListado_ViajaEnteraASql()
    {
        var padron = await app.CrearClienteAsync();
        await app.CrearViajeAsync(padron.Id, origen: "Rosario", destino: "Córdoba");

        var capturadas = new ConcurrentQueue<string>();

        // Una fábrica derivada con un capturador de log: EF registra el SQL de cada comando en la
        // categoría `Microsoft.EntityFrameworkCore.Database.Command`.
        using var conCaptura = app.WithWebHostBuilder(builder =>
            builder.ConfigureServices(servicios => servicios.AddLogging(registro =>
            {
                registro.SetMinimumLevel(LogLevel.Information);
                registro.AddProvider(new CapturadorDeSql(capturadas));
            })));

        var cliente = await AutenticarAsync(conCaptura);

        capturadas.Clear();

        var respuesta = await cliente.GetAsync(
            $"/api/viajes?clienteId={padron.Id}&busqueda=cordoba&estado=pendiente&pagina=1");

        respuesta.EnsureSuccessStatusCode();

        var sql = capturadas.FirstOrDefault(texto =>
            texto.Contains("[Viajes]", StringComparison.Ordinal) &&
            texto.Contains("OFFSET", StringComparison.Ordinal));

        Assert.NotNull(sql);

        // La derivación de `demorado` viaja como subconsulta correlacionada al historial, no como un
        // recorrido de filas traídas a memoria (research §6).
        Assert.Contains("CambiosDeEstadoViaje", sql, StringComparison.Ordinal);

        // La búsqueda usa la colación explícita, que es lo que la hace insensible a acentos.
        Assert.Contains("Latin1_General_CI_AI", sql, StringComparison.Ordinal);

        // Y la paginación se resolvió en la base: 20 filas pedidas, no filtradas después.
        Assert.Contains("FETCH NEXT", sql, StringComparison.Ordinal);
    }

    private static async Task<HttpClient> AutenticarAsync(WebApplicationFactory<Program> fabrica)
    {
        var cliente = fabrica.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false,
        });

        var respuesta = await cliente.PostAsJsonAsync("/api/auth/login", new
        {
            username = GT.Infrastructure.DatosIniciales.SembradorInicial.UsernameAdministrador,
            password = AplicacionDePrueba.PasswordAdministrador,
        });

        respuesta.EnsureSuccessStatusCode();

        return cliente;
    }

    /// <summary>Guarda el texto de cada comando SQL que EF registra.</summary>
    private sealed class CapturadorDeSql(ConcurrentQueue<string> destino) : ILoggerProvider
    {
        private const string CategoriaDeComandos = "Microsoft.EntityFrameworkCore.Database.Command";

        public ILogger CreateLogger(string categoryName) =>
            categoryName == CategoriaDeComandos
                ? new LoggerDeComandos(destino)
                : Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;

        public void Dispose()
        {
        }

        private sealed class LoggerDeComandos(ConcurrentQueue<string> destino) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

            public void Log<TState>(
                LogLevel logLevel,
                EventId eventId,
                TState state,
                Exception? exception,
                Func<TState, Exception?, string> formatter) =>
                destino.Enqueue(formatter(state, exception));
        }
    }
}
