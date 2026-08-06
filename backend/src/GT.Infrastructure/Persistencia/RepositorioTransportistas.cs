using GT.Application.Choferes.Transportistas;
using GT.Domain.Choferes;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public class RepositorioTransportistas(GtDbContext contexto) : IRepositorioTransportistas
{
    public Task<bool> ExisteCuitAsync(string cuitNormalizado, int? idAExcluir, CancellationToken cancelacion)
    {
        var consulta = contexto.Transportistas.AsQueryable();

        if (idAExcluir.HasValue)
        {
            consulta = consulta.Where(t => t.Id != idAExcluir.Value);
        }

        return consulta.AnyAsync(t => t.Cuit == cuitNormalizado, cancelacion);
    }

    public Task AgregarAsync(Transportista transportista, CancellationToken cancelacion)
    {
        contexto.Transportistas.Add(transportista);
        return Task.CompletedTask;
    }

    public async Task GuardarCambiosAsync(CancellationToken cancelacion)
    {
        try
        {
            await contexto.SaveChangesAsync(cancelacion);
        }
        catch (DbUpdateException excepcion) when (EsCuitDuplicado(excepcion))
        {
            throw new CuitDuplicadoException(excepcion);
        }
    }

    public async Task<List<TransportistaConChoferesActivos>> ConsultarAsync(
        string? textoBusqueda,
        string? cuitNormalizado,
        bool soloActivos,
        CancellationToken cancelacion)
    {
        var consulta = contexto.Transportistas.AsQueryable();

        if (soloActivos)
        {
            consulta = consulta.Where(t => t.Activo);
        }

        if (!string.IsNullOrWhiteSpace(textoBusqueda))
        {
            var porNombre = $"%{textoBusqueda}%";
            var porCuit = $"%{cuitNormalizado ?? textoBusqueda}%";

            consulta = consulta.Where(t =>
                EF.Functions.Like(t.Nombre, porNombre) ||
                EF.Functions.Like(t.Cuit, porCuit));
        }

        // La cantidad de choferes activos se cuenta en la misma consulta (FR-010): trae un número
        // por fila en vez de los choferes enteros.
        var filas = await consulta
            .OrderBy(t => t.Nombre)
            .ThenBy(t => t.Id)
            .Select(t => new
            {
                Transportista = t,
                ChoferesActivos = t.Choferes.Count(chofer => chofer.Activo)
            })
            .AsNoTracking()
            .ToListAsync(cancelacion);

        return filas
            .Select(fila => new TransportistaConChoferesActivos(fila.Transportista, fila.ChoferesActivos))
            .ToList();
    }

    public async Task<TransportistaConChoferesActivos?> ObtenerConChoferesActivosAsync(
        int id,
        CancellationToken cancelacion)
    {
        var fila = await contexto.Transportistas
            .Where(t => t.Id == id)
            .Select(t => new
            {
                Transportista = t,
                ChoferesActivos = t.Choferes.Count(chofer => chofer.Activo)
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(cancelacion);

        return fila is null
            ? null
            : new TransportistaConChoferesActivos(fila.Transportista, fila.ChoferesActivos);
    }

    public Task<Transportista?> ObtenerPorIdAsync(int id, CancellationToken cancelacion) =>
        contexto.Transportistas.FirstOrDefaultAsync(t => t.Id == id, cancelacion);

    private static bool EsCuitDuplicado(DbUpdateException excepcion) =>
        excepcion.InnerException is SqlException { Number: 2601 or 2627 } sql &&
        sql.Message.Contains("IX_Transportistas_Cuit", StringComparison.Ordinal);
}
