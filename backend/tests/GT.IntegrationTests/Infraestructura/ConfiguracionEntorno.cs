namespace GT.IntegrationTests.Infraestructura;

/// <summary>
/// Lee la configuración que los tests necesitan para hablar con el SQL Server del compose.
///
/// `dotnet test` no carga el archivo `.env`, así que se lee a mano desde la raíz del repositorio.
/// De este modo el flujo documentado en CLAUDE.md —`podman compose up -d` y después `dotnet test`—
/// funciona sin ningún paso extra.
/// </summary>
public static class ConfiguracionEntorno
{
    public static string PasswordSqlServer =>
        Leer("GT_SQL_PASSWORD")
        ?? throw new InvalidOperationException(
            "Falta GT_SQL_PASSWORD. Los tests de integración necesitan el SQL Server del compose: " +
            "copiá .env.template a .env, completá la variable y levantá el sistema con " +
            "`podman compose up -d`.");

    /// <summary>
    /// Cadena de conexión a una base con nombre propio, para aislar cada corrida.
    ///
    /// Se usa 127.0.0.1 y no "localhost" a propósito: el compose publica el puerto en IPv4
    /// (0.0.0.0), mientras que en Windows "localhost" resuelve primero a ::1. Con el nombre, el
    /// cliente intenta por IPv6, no encuentra a nadie escuchando y espera hasta agotar el tiempo.
    /// </summary>
    public static string CadenaDeConexion(string nombreBase) =>
        $"Server=127.0.0.1,1433;Database={nombreBase};User Id=sa;" +
        $"Password={PasswordSqlServer};TrustServerCertificate=True;Encrypt=True";

    public static string CadenaDeConexionMaestra() => CadenaDeConexion("master");

    private static string? Leer(string clave)
    {
        var deEntorno = Environment.GetEnvironmentVariable(clave);

        if (!string.IsNullOrWhiteSpace(deEntorno))
        {
            return deEntorno;
        }

        var archivo = BuscarArchivoEnv();

        if (archivo is null)
        {
            return null;
        }

        foreach (var linea in File.ReadAllLines(archivo))
        {
            var limpia = linea.Trim();

            if (limpia.StartsWith('#') || !limpia.StartsWith(clave + "="))
            {
                continue;
            }

            var valor = limpia[(clave.Length + 1)..].Trim().Trim('"');

            if (!string.IsNullOrWhiteSpace(valor))
            {
                return valor;
            }
        }

        return null;
    }

    private static string? BuscarArchivoEnv()
    {
        var directorio = new DirectoryInfo(AppContext.BaseDirectory);

        while (directorio is not null)
        {
            var candidato = Path.Combine(directorio.FullName, ".env");

            if (File.Exists(candidato))
            {
                return candidato;
            }

            directorio = directorio.Parent;
        }

        return null;
    }
}
