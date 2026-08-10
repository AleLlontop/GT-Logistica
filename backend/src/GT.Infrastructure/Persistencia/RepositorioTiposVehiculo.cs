using GT.Application.Flota.TiposVehiculo;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

using EntidadTipoVehiculo = GT.Domain.Flota.TipoVehiculo;

namespace GT.Infrastructure.Persistencia;

public class RepositorioTiposVehiculo(GtDbContext contexto) : IRepositorioTiposVehiculo
{
    public async Task<List<TipoConVehiculos>> ConsultarAsync(
        bool soloActivos,
        CancellationToken cancelacion)
    {
        var consulta = contexto.TiposVehiculo.AsQueryable();

        if (soloActivos)
        {
            consulta = consulta.Where(tipo => tipo.Activo);
        }

        var filas = await consulta
            .OrderBy(tipo => tipo.Nombre)
            .ThenBy(tipo => tipo.Id)
            // La cantidad se cuenta en la misma consulta: trae un número por fila en vez de los
            // vehículos enteros. Cuenta **todos**, activos e inactivos (FR-010).
            .Select(tipo => new { Tipo = tipo, Vehiculos = tipo.Vehiculos.Count })
            .AsNoTracking()
            .ToListAsync(cancelacion);

        return filas.Select(fila => new TipoConVehiculos(fila.Tipo, fila.Vehiculos)).ToList();
    }

    public async Task<TipoConVehiculos?> ObtenerConVehiculosAsync(int id, CancellationToken cancelacion)
    {
        var fila = await contexto.TiposVehiculo
            .Where(tipo => tipo.Id == id)
            .Select(tipo => new { Tipo = tipo, Vehiculos = tipo.Vehiculos.Count })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancelacion);

        return fila is null ? null : new TipoConVehiculos(fila.Tipo, fila.Vehiculos);
    }

    public Task<EntidadTipoVehiculo?> ObtenerPorIdAsync(int id, CancellationToken cancelacion) =>
        contexto.TiposVehiculo.FirstOrDefaultAsync(tipo => tipo.Id == id, cancelacion);

    public Task<bool> ExisteNombreAsync(string nombre, int? idAExcluir, CancellationToken cancelacion) =>
        contexto.TiposVehiculo.AnyAsync(
            tipo => tipo.Nombre == nombre && (idAExcluir == null || tipo.Id != idAExcluir),
            cancelacion);

    public Task AgregarAsync(EntidadTipoVehiculo tipo, CancellationToken cancelacion)
    {
        contexto.TiposVehiculo.Add(tipo);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Traduce la violación del índice único a una excepción de la capa de aplicación, para que ni
    /// EF Core ni SqlClient se filtren hacia arriba (convención [003]).
    /// </summary>
    public async Task GuardarCambiosAsync(CancellationToken cancelacion)
    {
        try
        {
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException excepcion) when (EsNombreDuplicado(excepcion))
        {
            throw new NombreDeTipoVehiculoDuplicadoException(excepcion);
        }
    }

    private static bool EsNombreDuplicado(DbUpdateException excepcion) =>
        excepcion.InnerException is SqlException { Number: 2601 or 2627 } sql &&
        sql.Message.Contains("IX_TiposVehiculo_Nombre", StringComparison.Ordinal);
}
