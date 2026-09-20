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
    /// <param name="tamanioPagina">
    /// <c>null</c> es el tamaño de siempre y <b>ninguna llamada existente cambia</b>. El reporte del
    /// Módulo 12 lo usa para traerse todas las filas del filtro llamando a <b>esta misma consulta</b>:
    /// así el orden no se replica, se hereda (research §3).
    /// </param>
    public Task<PaginaDe<ViajeListado>> EjecutarAsync(
        FiltrosDeViajes filtros,
        int? tamanioPagina = null,
        CancellationToken cancelacion = default) =>
        viajes.ConsultarAsync(filtros, MomentoDeLectura.Desde(reloj), tamanioPagina, cancelacion);
}
