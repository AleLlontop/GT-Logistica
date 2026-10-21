using GT.Application.Choferes;

namespace GT.Application.Reportes.Analisis;

public interface IConsultaLiquidacionesAnalisis
{
    Task<AnalisisLiquidaciones> LiquidacionesAsync(FiltroLiquidacionesAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default);
    Task<AnalisisPagos> PagosAsync(FiltroPagosAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default);
    Task<GruposCantidadAnalisis> EstadosLiquidacionesAsync(FiltroGrupoAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default);
    Task<GruposImporteAnalisis> TransportistasLiquidacionesAsync(FiltroGrupoAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default);
    Task<PaginaDe<ReferenciaAnalisis>> ReferenciasTransportistasLiquidacionesAsync(string? busqueda, FiltroPaginaAnalisis pagina, CancellationToken ct = default);
}
