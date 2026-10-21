using GT.Application.Choferes;

namespace GT.Application.Reportes.Analisis;

/// <summary>Cada operación materializa una foto de lectura; nunca devuelve consultas diferidas ni entidades.</summary>
public interface IRepositorioAnalisisReportes : IConsultaViajesAnalisis, IConsultaFacturacionAnalisis, IConsultaLiquidacionesAnalisis, IConsultaCajaAnalisis, IConsultaFlotaAnalisis, IConsultaVencimientosAnalisis, IConsultaDashboardAnalisis
{
    Task<GruposCantidadAnalisis> GruposCantidadAsync(FiltroGrupoAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default);
    Task<GruposImporteAnalisis> GruposImporteAsync(FiltroGrupoAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default);
    Task<PaginaDe<ReferenciaAnalisis>> ReferenciasAsync(FiltroReferenciaAnalisis filtro, CancellationToken ct = default);
    /// <summary>Count bajo la misma foto; rechazar sobre tope antes de materializar filas y cerrar la foto antes de renderizar.</summary>
    Task<ReporteTabular> MaterializarExportacionAsync(FiltroTemporalAnalisis filtro, DateTimeOffset instante, string autor, CancellationToken ct = default);
}
