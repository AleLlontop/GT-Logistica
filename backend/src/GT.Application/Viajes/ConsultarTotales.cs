namespace GT.Application.Viajes;

/// <summary>
/// Los totales por cliente y por transportista en un período (FR-046, FR-046a, FR-047).
///
/// <b>El rango de fechas es obligatorio</b>, y no un filtro más con valor por defecto: sin rango no
/// se calcula nada y la pantalla dice que falta elegirlo. Un total "de todo" no responde ninguna
/// pregunta real —Administración le arma a Gerencia el resumen de un mes— y calcular sobre todo el
/// historial por defecto sería el número más caro y menos útil.
/// </summary>
public class ConsultarTotales(IRepositorioViajes viajes)
{
    public async Task<ResultadoTotales> EjecutarAsync(
        DateOnly? desde,
        DateOnly? hasta,
        CancellationToken cancelacion = default)
    {
        if (desde is null || hasta is null)
        {
            return new ResultadoTotales(ErrorViaje.RangoDeFechasRequerido);
        }

        var totales = await viajes.ConsultarTotalesAsync(desde.Value, hasta.Value, cancelacion);

        return new ResultadoTotales(ErrorViaje.Ninguno, totales);
    }
}

public record ResultadoTotales(ErrorViaje Error, TotalesDelPeriodo? Totales = null)
{
    public bool Exitoso => Error is ErrorViaje.Ninguno;
}
