using GT.Application.Choferes;

namespace GT.Application.Reportes.Analisis;

public interface IConsultaCajaAnalisis
{
    Task<AnalisisCaja> CajaAsync(FiltroCajaAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default);
    Task<GruposImporteAnalisis> TiposCajaAsync(FiltroGrupoAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default);
    Task<PaginaDe<ReferenciaAnalisis>> ReferenciasCajasAsync(string? busqueda, FiltroPaginaAnalisis pagina, CancellationToken ct = default);
}
