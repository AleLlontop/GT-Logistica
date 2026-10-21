using System.Data;
using GT.Application.Reportes.Analisis;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace GT.Infrastructure.Persistencia;

/// <summary>Diagnóstico de la base conectada; no modifica opciones SQL ni imprime configuración.</summary>
public sealed class DetectorSnapshotAnalisis(GtDbContext db)
{
    public async Task<DiagnosticoSnapshotAnalisis> ConsultarAsync(CancellationToken ct = default)
    {
        var conexion = db.Database.GetDbConnection();
        var abrir = conexion.State != ConnectionState.Open;
        if (abrir) await db.Database.OpenConnectionAsync(ct);
        try
        {
            await using var comando = conexion.CreateCommand();
            comando.CommandText = "SELECT snapshot_isolation_state, snapshot_isolation_state_desc, is_read_committed_snapshot_on FROM sys.databases WHERE database_id = DB_ID()";
            comando.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
            await using var lector = await comando.ExecuteReaderAsync(ct);
            if (!await lector.ReadAsync(ct)) throw new InvalidOperationException("No pudimos consultar el estado de Snapshot para análisis.");
            var estado = (EstadoSnapshotAnalisis)lector.GetByte(0);
            if (!Enum.IsDefined(estado)) throw new InvalidOperationException("El estado de Snapshot no es reconocido.");
            return new(estado, lector.GetString(1), lector.GetBoolean(2));
        }
        finally
        {
            if (abrir) await db.Database.CloseConnectionAsync();
        }
    }
}
