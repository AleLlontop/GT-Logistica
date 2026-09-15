namespace GT.Application.Adelantos;

/// <summary>
/// Listado paginado con su total adelantado (FR-013 a FR-018). Todo lo que decide qué filas salen vive en
/// la consulta del repositorio; acá sólo se rechaza el rango invertido, <b>sin consultar</b> (FR-015).
/// </summary>
public class ConsultarAdelantos(IRepositorioAdelantos adelantos)
{
    public async Task<ResultadoConsultaAdelantos> EjecutarAsync(
        FiltrosDeAdelantos filtros,
        CancellationToken cancelacion = default)
    {
        if (filtros is { Desde: { } desde, Hasta: { } hasta } && desde > hasta)
        {
            return new ResultadoConsultaAdelantos(
                null,
                ResultadoAdelanto.Rechazo(ErrorAdelanto.RangoInvalido, MensajesAdelantos.RangoInvalido, "desde"));
        }

        return new ResultadoConsultaAdelantos(await adelantos.ConsultarAsync(filtros, cancelacion), null);
    }
}
