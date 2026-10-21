using GT.Application.Choferes;

namespace GT.Application.Reportes.Analisis;

public interface IConsultaFacturacionAnalisis
{
    Task<AnalisisFacturacion> FacturacionAsync(FiltroFacturacionAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default);
    Task<AnalisisCobros> CobrosAsync(FiltroCobrosAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default);
    Task<GruposCantidadAnalisis> EstadosFacturacionAsync(FiltroGrupoAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default);
    Task<GruposImporteAnalisis> ClientesFacturacionAsync(FiltroGrupoAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default);
    Task<PaginaDe<ReferenciaAnalisis>> ReferenciasClientesFacturacionAsync(string? busqueda, FiltroPaginaAnalisis pagina, CancellationToken ct = default);
}
