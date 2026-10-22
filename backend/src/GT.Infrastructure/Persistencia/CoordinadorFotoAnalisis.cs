using System.Data;
using GT.Application.Reportes.Analisis;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

/// <summary>Una foto materializada por respuesta. No implementa reintentos ni transacciones de escritura.</summary>
public sealed class CoordinadorFotoAnalisis(GtDbContext db, ReadinessAnalisisReportes readiness)
{
    public async Task<T> LeerAsync<T>(Func<GtDbContext, CancellationToken, Task<T>> lectura, CancellationToken ct = default)
    {
        if (db.Database.CurrentTransaction is not null)
            throw new InvalidOperationException("La foto de análisis no admite una transacción anidada.");
        await readiness.AsegurarAsync(ct);
        try
        {
            await using var transaccion = await db.Database.BeginTransactionAsync(IsolationLevel.Snapshot, ct);
            var resultado = await lectura(db, ct);
            if (resultado is IQueryable)
                throw new InvalidOperationException("El análisis debe materializar los datos antes de cerrar su foto.");
            await transaccion.CommitAsync(ct);
            return resultado;
        }
        catch (SqlException error) when (error.Number is -2 or 1205 or 3951 or 3952)
        {
            throw new AnalisisNoDisponibleException(error);
        }
    }
}
