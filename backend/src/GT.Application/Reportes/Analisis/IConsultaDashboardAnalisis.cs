namespace GT.Application.Reportes.Analisis;

public interface IConsultaDashboardAnalisis
{
    Task<DashboardAnalisis> DashboardAsync(PeriodoAnalisis periodo, IReadOnlySet<SeccionReporte> autorizadas, DateTimeOffset instante, CancellationToken ct = default);
}

public sealed class ConsultarDashboard(IConsultaDashboardAnalisis repositorio, TimeProvider reloj)
{
    public Task<DashboardAnalisis> EjecutarAsync(PeriodoAnalisis periodo, IReadOnlySet<SeccionReporte> autorizadas, CancellationToken ct = default) =>
        repositorio.DashboardAsync(periodo, autorizadas, reloj.GetUtcNow(), ct);
}
