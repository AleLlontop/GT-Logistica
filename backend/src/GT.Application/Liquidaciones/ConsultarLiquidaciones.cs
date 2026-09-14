using GT.Application.Choferes;

namespace GT.Application.Liquidaciones;

/// <summary>
/// Listado paginado (FR-021 a FR-024). Todo lo que decide qué filas salen vive en la consulta del
/// repositorio: los cuatro filtros y el orden.
/// </summary>
public class ConsultarLiquidaciones(IRepositorioLiquidaciones liquidaciones)
{
    public Task<PaginaDe<LiquidacionListado>> EjecutarAsync(
        FiltrosDeLiquidaciones filtros,
        CancellationToken cancelacion = default) =>
        liquidaciones.ConsultarAsync(filtros, cancelacion);
}
