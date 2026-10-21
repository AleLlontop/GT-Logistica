using GT.Application.Reportes.Analisis;
using Microsoft.Data.SqlClient;

namespace GT.Infrastructure.Persistencia;

/// <summary>Precondición sólo de nuevas lecturas015. No afecta el arranque ni las rutas legacy.</summary>
public sealed class ReadinessAnalisisReportes(DetectorSnapshotAnalisis detector)
{
    public async Task AsegurarAsync(CancellationToken ct = default)
    {
        try
        {
            if (!(await detector.ConsultarAsync(ct)).Disponible)
                throw new AnalisisNoDisponibleException();
        }
        catch (SqlException error) { throw new AnalisisNoDisponibleException(error); }
    }
}
