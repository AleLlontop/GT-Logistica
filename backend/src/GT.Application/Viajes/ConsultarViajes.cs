using GT.Application.Choferes;

namespace GT.Application.Viajes;

/// <summary>
/// Listado paginado de viajes (FR-040 a FR-044).
///
/// Todo lo que decide qué filas salen vive en la consulta del repositorio: los filtros, la búsqueda,
/// la exclusión de anulados y las dos señales derivadas. Acá sólo se le pasa el momento de lectura,
/// que sale del <c>TimeProvider</c> registrado y no de <c>DateTime.UtcNow</c>, para que un test pueda
/// fijarlo.
/// </summary>
public class ConsultarViajes(IRepositorioViajes viajes, TimeProvider reloj)
{
    public Task<PaginaDe<ViajeListado>> EjecutarAsync(
        FiltrosDeViajes filtros,
        CancellationToken cancelacion = default) =>
        viajes.ConsultarAsync(filtros, MomentoDeLectura.Desde(reloj), cancelacion);
}
