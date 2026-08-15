using GT.Application.Choferes;

namespace GT.Application.Facturacion;

/// <summary>
/// Listado paginado de facturas (FR-057 a FR-059).
///
/// Todo lo que decide qué filas salen vive en la consulta del repositorio: los cinco filtros, el estado
/// derivado y el orden. Acá sólo se le pasa el día en curso, que sale del <c>TimeProvider</c> registrado
/// y no de <c>DateTime.UtcNow</c>, para que un test pueda fijarlo en vez de esperar a que venza una
/// factura (convención [005]).
/// </summary>
public class ConsultarFacturas(IRepositorioFacturas facturas, TimeProvider reloj)
{
    public Task<PaginaDe<FacturaListado>> EjecutarAsync(
        FiltrosDeFacturas filtros,
        CancellationToken cancelacion = default) =>
        facturas.ConsultarAsync(
            filtros,
            Domain.Choferes.FechaHoyArgentina.Desde(reloj.GetUtcNow()),
            cancelacion);
}
