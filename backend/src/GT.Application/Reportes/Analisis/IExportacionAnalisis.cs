namespace GT.Application.Reportes.Analisis;

/// <summary>Materializa el conjunto filtrado completo en una foto, antes de dibujar cualquier formato.</summary>
public interface IExportacionAnalisis
{
    Task<ReporteTabular> ExportarViajesAsync(FiltroViajesAnalisis filtro, DateTimeOffset instante, string autor, CancellationToken ct = default);
    Task<ReporteTabular> ExportarCajaAsync(FiltroCajaAnalisis filtro, DateTimeOffset instante, string autor, CancellationToken ct = default);
    Task<ReporteTabular> ExportarFacturacionAsync(FiltroFacturacionAnalisis filtro, DateTimeOffset instante, string autor, CancellationToken ct = default);
    Task<ReporteTabular> ExportarCobrosAsync(FiltroCobrosAnalisis filtro, DateTimeOffset instante, string autor, CancellationToken ct = default);
    Task<ReporteTabular> ExportarLiquidacionesAsync(FiltroLiquidacionesAnalisis filtro, DateTimeOffset instante, string autor, CancellationToken ct = default);
    Task<ReporteTabular> ExportarPagosAsync(FiltroPagosAnalisis filtro, DateTimeOffset instante, string autor, CancellationToken ct = default);
    Task<ReporteTabular> ExportarFlotaAsync(FiltroFlotaAnalisis filtro, DateTimeOffset instante, string autor, CancellationToken ct = default);
    Task<ReporteTabular> ExportarVencimientosAsync(SeccionReporte panel, DateTimeOffset instante, string autor, CancellationToken ct = default);
}

public sealed class TopeExportacionAnalisisException(int filas, int tope)
    : Exception(MensajesDeReporte.TopeDeFilasSuperado(filas, tope))
{
    public int Filas { get; } = filas;
    public int Tope { get; } = tope;
}
