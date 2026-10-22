using GT.Application.Choferes;

namespace GT.Application.Reportes.Analisis;

/// <summary>Superficie implementable por perspectiva, sin publicar operaciones todavía pendientes.</summary>
public interface IConsultaViajesAnalisis
{
    Task<AnalisisViajes> ViajesAsync(FiltroViajesAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default);
    Task<GruposCantidadAnalisis> EstadosViajesAsync(FiltroGrupoAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default);
    Task<GruposImporteAnalisis> ImportesViajesAsync(FiltroGrupoAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default);
    Task<PaginaDe<ReferenciaAnalisis>> ReferenciasViajesAsync(bool transportistas, string? busqueda, FiltroPaginaAnalisis pagina, CancellationToken ct = default);
}
