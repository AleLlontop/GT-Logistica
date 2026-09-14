using GT.Domain.Liquidaciones;

namespace GT.Application.Liquidaciones;

/// <summary>
/// Genera una liquidación en <c>pendiente</c> con <b>exactamente</b> los viajes revisados (FR-011, FR-015).
///
/// No pide confirmación: se deshace editándola o anulándola (research §7).
/// </summary>
public class GenerarLiquidacion(
    IRepositorioLiquidaciones liquidaciones,
    ValidadorDeViajes validador,
    ConsultarDetalleLiquidacion detalles,
    TimeProvider reloj)
{
    public async Task<ResultadoLiquidacion> EjecutarAsync(
        GeneracionRequest? peticion,
        int usuarioId,
        CancellationToken cancelacion = default)
    {
        var (rechazo, transportista) = await validador.ValidarArmadoAsync(
            peticion?.TransportistaId,
            peticion?.Mes,
            peticion?.Anio,
            cancelacion);

        if (rechazo is not null)
        {
            return rechazo;
        }

        var mes = peticion!.Mes!.Value;
        var anio = peticion.Anio!.Value;

        var (rechazoDeViajes, viajes, total) = await validador.ValidarViajesAsync(
            transportista!.Id,
            mes,
            anio,
            peticion.ViajeIds,
            liquidacionPropiaId: null,
            cancelacion);

        if (rechazoDeViajes is not null)
        {
            return rechazoDeViajes;
        }

        // FR-012a: un viaje en cero se liquida junto con otros; todos en cero no son una liquidación.
        if (total == 0m)
        {
            return ResultadoLiquidacion.Rechazo(
                ErrorLiquidacion.TotalEnCero,
                MensajesLiquidaciones.TotalEnCeroAlGenerar,
                "viajeIds");
        }

        var liquidacion = new Liquidacion
        {
            TransportistaId = transportista.Id,
            PeriodoMes = (byte)mes,
            PeriodoAnio = (short)anio,
            ImporteTotal = total,
        };

        var viajeIds = viajes.Select(viaje => viaje.Id).Order().ToList();

        int id;

        try
        {
            id = await liquidaciones.GenerarAsync(
                liquidacion,
                viajeIds,
                usuarioId,
                reloj.GetUtcNow().UtcDateTime,
                cancelacion);
        }
        catch (ViajeYaLiquidadoException)
        {
            // La carrera de SC-002: los dos pasaron la consulta previa y el índice cortó a este. Se
            // averigua qué viaje y en qué liquidación quedó, para nombrarlos (research §12.5).
            var vinculos = await liquidaciones.ConsultarVinculosVigentesAsync(viajeIds, cancelacion);

            return vinculos.Count == 0
                ? ResultadoLiquidacion.Rechazo(ErrorLiquidacion.DatosInvalidos, MensajesLiquidaciones.DatosInvalidos)
                : ValidadorDeViajes.RechazoPorYaLiquidados(vinculos, alEditar: false);
        }

        return ResultadoLiquidacion.Exito((await detalles.EjecutarAsync(id, cancelacion))!);
    }
}
