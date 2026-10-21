using GT.Application.Choferes;

namespace GT.Application.Reportes.Analisis;

public interface IConsultaFlotaAnalisis
{
    Task<AnalisisFlota> FlotaAsync(FiltroFlotaAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default);
    Task<GruposCantidadAnalisis> GruposFlotaAsync(FiltroGrupoAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default);
    Task<PaginaDe<ReferenciaAnalisis>> ReferenciasFlotaAsync(ReferenciaDeAnalisis recurso, string? busqueda, FiltroPaginaAnalisis pagina, CancellationToken ct = default);
}
