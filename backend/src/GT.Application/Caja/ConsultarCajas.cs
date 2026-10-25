using GT.Application.Choferes;

namespace GT.Application.Caja;

/// <summary>Listado paginado de las cajas de todos los responsables (FR-026). Sin filtros: la spec no pide ninguno.</summary>
public class ConsultarCajas(IRepositorioCaja cajas)
{
    public Task<PaginaDe<CajaListado>> EjecutarAsync(int pagina, CancellationToken cancelacion = default) =>
        cajas.ConsultarCajasAsync(Math.Max(pagina, 1), cancelacion);

    /// <summary>Todas las cajas de un responsable, filtradas en SQL: el historial de la ficha de empleado.</summary>
    public Task<IReadOnlyList<CajaListado>> DeResponsableAsync(int usuarioId, CancellationToken cancelacion = default) =>
        cajas.ConsultarCajasDeResponsableAsync(usuarioId, cancelacion);
}
