using GT.Domain.Liquidaciones;

namespace GT.Application.Liquidaciones;

/// <summary>
/// Quita y agrega viajes de una liquidación <c>pendiente</c>, sin pagos y con su transportista todavía
/// liquidable (FR-045 a FR-052). Transportista, período, número, fecha de generación y estado no cambian.
///
/// <b>La diferencia se calcula contra la composición leída</b>, antes de la transacción. Lo que la vuelve
/// segura es la condición de <c>Version</c> del <c>UPDATE</c>: si otra edición cambió la composición en el
/// medio, ésta se rechaza antes de tocar un solo vínculo (data-model §Editar).
/// </summary>
public class EditarLiquidacion(
    IRepositorioLiquidaciones liquidaciones,
    ValidadorDeViajes validador,
    ConsultarDetalleLiquidacion detalles,
    TimeProvider reloj)
{
    public async Task<ResultadoLiquidacion> EjecutarAsync(
        int id,
        EdicionRequest? peticion,
        int usuarioId,
        CancellationToken cancelacion = default)
    {
        var liquidacion = await liquidaciones.ObtenerDetalleAsync(id, cancelacion);

        if (liquidacion is null)
        {
            return ResultadoLiquidacion.Rechazo(ErrorLiquidacion.NoEncontrada, MensajesLiquidaciones.NoEncontrada);
        }

        if (peticion?.Version is not { } versionAbierta)
        {
            return ResultadoLiquidacion.Rechazo(
                ErrorLiquidacion.DatosInvalidos,
                MensajesLiquidaciones.DatosInvalidos,
                "version");
        }

        var cuitEmisora = await liquidaciones.ObtenerCuitEmpresaEmisoraAsync(cancelacion);

        if (RechazoSiNoEditable(liquidacion, cuitEmisora) is { } noEditable)
        {
            return noEditable;
        }

        if (versionAbierta != liquidacion.Version)
        {
            return Modificada(liquidacion);
        }

        // Contra el transportista y el período **de la liquidación**, y sin contar como conflicto a los
        // viajes que ya le pertenecen (FR-046, FR-049).
        var (rechazo, viajes, total) = await validador.ValidarViajesAsync(
            liquidacion.TransportistaId,
            liquidacion.PeriodoMes,
            liquidacion.PeriodoAnio,
            peticion.ViajeIds,
            liquidacion.Id,
            cancelacion);

        if (rechazo is not null)
        {
            return rechazo;
        }

        var actuales = liquidacion.Viajes
            .Where(vinculo => vinculo.Vigente)
            .Select(vinculo => vinculo.ViajeId)
            .ToHashSet();

        var finales = viajes.Select(viaje => viaje.Id).ToHashSet();

        var quitados = actuales.Except(finales).Order().ToList();
        var agregados = finales.Except(actuales).Order().ToList();

        // Un conjunto igual al actual no es una edición: no se escribe nada ni se sube la versión.
        if (quitados.Count == 0 && agregados.Count == 0)
        {
            return ResultadoLiquidacion.Exito(ConsultarDetalleLiquidacion.Armar(liquidacion, cuitEmisora));
        }

        if (total == 0m)
        {
            return ResultadoLiquidacion.Rechazo(
                ErrorLiquidacion.TotalEnCero,
                MensajesLiquidaciones.TotalEnCeroAlEditar,
                "viajeIds");
        }

        bool aplicada;

        try
        {
            aplicada = await liquidaciones.EditarAsync(
                id,
                versionAbierta,
                total,
                quitados,
                agregados,
                usuarioId,
                reloj.GetUtcNow().UtcDateTime,
                cancelacion);
        }
        catch (ViajeYaLiquidadoException)
        {
            var vinculos = (await liquidaciones.ConsultarVinculosVigentesAsync(agregados, cancelacion))
                .Where(vinculo => vinculo.LiquidacionId != id)
                .ToList();

            return vinculos.Count == 0
                ? ResultadoLiquidacion.Rechazo(ErrorLiquidacion.DatosInvalidos, MensajesLiquidaciones.DatosInvalidos)
                : ValidadorDeViajes.RechazoPorYaLiquidados(vinculos, alEditar: true);
        }

        if (!aplicada)
        {
            // El `UPDATE` no afectó ninguna fila. Se relee para decir el motivo concreto: un pago o una
            // anulación tienen el suyo, y "otro usuario guardó cambios" queda sólo para otra edición
            // (FR-048, spec §Clarifications).
            var actual = await liquidaciones.ObtenerDetalleAsync(id, cancelacion);

            if (actual is null)
            {
                return ResultadoLiquidacion.Rechazo(ErrorLiquidacion.NoEncontrada, MensajesLiquidaciones.NoEncontrada);
            }

            return RechazoSiNoEditable(actual, cuitEmisora) ?? Modificada(actual);
        }

        return ResultadoLiquidacion.Exito((await detalles.EjecutarAsync(id, cancelacion))!);
    }

    private static ResultadoLiquidacion? RechazoSiNoEditable(Liquidacion liquidacion, string? cuitEmisora)
    {
        var motivo = ReglasDeLiquidacion.MotivoNoEditable(
            liquidacion.Estado,
            liquidacion.ImportePagado,
            ConsultarDetalleLiquidacion.TransportistaLiquidable(liquidacion.Transportista!, cuitEmisora));

        return motivo is null
            ? null
            : new ResultadoLiquidacion(
                ErrorLiquidacion.LiquidacionNoEditable,
                Mensaje: MensajesLiquidaciones.NoEditable(
                    NumerosVisibles.Liquidacion(liquidacion.Numero),
                    motivo.Value),
                Motivo: motivo,
                CantidadOrdenesDePago: motivo is MotivoDeBloqueoLiquidacion.ConOrdenesDePago
                    ? liquidacion.OrdenesDePago.Count
                    : null,
                ImportePagado: motivo is MotivoDeBloqueoLiquidacion.ConOrdenesDePago
                    ? liquidacion.ImportePagado
                    : null);
    }

    private static ResultadoLiquidacion Modificada(Liquidacion liquidacion) =>
        ResultadoLiquidacion.Rechazo(
            ErrorLiquidacion.LiquidacionModificada,
            MensajesLiquidaciones.LiquidacionModificada(NumerosVisibles.Liquidacion(liquidacion.Numero)));
}
