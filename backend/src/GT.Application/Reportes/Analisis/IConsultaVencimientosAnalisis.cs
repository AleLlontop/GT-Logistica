namespace GT.Application.Reportes.Analisis;

public interface IConsultaVencimientosAnalisis
{
    Task<AnalisisVencimientosChoferes> VencimientosChoferesAsync(FiltroPaginaAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default);
    Task<AnalisisVencimientosFlota> VencimientosFlotaAsync(FiltroPaginaAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default);
    Task<AnalisisVencimientosFacturas> VencimientosFacturasAsync(FiltroPaginaAnalisis filtro, ContextoAnalisis contexto, CancellationToken ct = default);
}
