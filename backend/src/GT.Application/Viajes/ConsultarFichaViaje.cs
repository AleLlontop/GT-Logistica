namespace GT.Application.Viajes;

/// <summary>
/// Ficha completa de un viaje (FR-045), con su historial de cambios de estado del más viejo al más
/// nuevo, empezando por el alta (FR-035).
/// </summary>
public class ConsultarFichaViaje(IRepositorioViajes viajes, TimeProvider reloj)
{
    public async Task<ViajeDetalle?> EjecutarAsync(int id, CancellationToken cancelacion = default)
    {
        var viaje = await viajes.ObtenerFichaAsync(id, cancelacion);

        return viaje is null ? null : ViajeDetalle.Desde(viaje, MomentoDeLectura.Desde(reloj));
    }
}
