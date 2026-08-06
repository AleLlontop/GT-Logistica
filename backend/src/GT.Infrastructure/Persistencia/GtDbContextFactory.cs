using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace GT.Infrastructure.Persistencia;

/// <summary>
/// Fábrica usada únicamente por las herramientas de EF Core al generar migraciones desde la línea
/// de comandos. No participa en la ejecución de la aplicación: en tiempo real el contexto se
/// registra desde <c>GT.Api/Program.cs</c> con la cadena de conexión de configuración.
/// </summary>
public class GtDbContextFactory : IDesignTimeDbContextFactory<GtDbContext>
{
    public GtDbContext CreateDbContext(string[] args)
    {
        var opciones = new DbContextOptionsBuilder<GtDbContext>()
            .UseSqlServer("Server=(local);Database=GtLogistica;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;

        return new GtDbContext(opciones);
    }
}
