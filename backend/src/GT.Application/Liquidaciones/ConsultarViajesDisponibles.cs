using GT.Application.Viajes;

namespace GT.Application.Liquidaciones;

/// <summary>
/// Los viajes disponibles de un transportista en un período (FR-004). Los usa la generación y también la
/// edición, que toma transportista y período de la liquidación (research §6).
/// </summary>
public class ConsultarViajesDisponibles(IRepositorioLiquidaciones liquidaciones, ValidadorDeViajes validador)
{
    public async Task<(ResultadoLiquidacion? Rechazo, IReadOnlyList<ViajeDisponible>? Viajes)> EjecutarAsync(
        int? transportistaId,
        int? mes,
        int? anio,
        CancellationToken cancelacion = default)
    {
        var (rechazo, transportista) = await validador.ValidarArmadoAsync(transportistaId, mes, anio, cancelacion);

        if (rechazo is not null)
        {
            return (rechazo, null);
        }

        var viajes = await liquidaciones.ConsultarDisponiblesAsync(
            transportista!.Id,
            mes!.Value,
            anio!.Value,
            cancelacion);

        return (null, [.. viajes.Select(viaje => new ViajeDisponible(
            viaje.Id,
            viaje.Numero,
            viaje.Fecha.ToString("yyyy-MM-dd"),
            viaje.Origen,
            viaje.Destino,
            NombresDeEstadoViaje.EnJson(viaje.Estado),
            viaje.Importe))]);
    }
}
