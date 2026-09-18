using GT.Application.Choferes;

namespace GT.Application.Caja;

/// <summary>Listado paginado de las cajas de todos los responsables (FR-026). Sin filtros: la spec no pide ninguno.</summary>
public class ConsultarCajas(IRepositorioCaja cajas)
{
    public Task<PaginaDe<CajaListado>> EjecutarAsync(int pagina, CancellationToken cancelacion = default) =>
        cajas.ConsultarCajasAsync(Math.Max(pagina, 1), cancelacion);
}
