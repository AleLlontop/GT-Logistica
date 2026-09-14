using GT.Domain.Choferes;
using GT.Domain.Liquidaciones;
using GT.Domain.Viajes;

namespace GT.Application.Liquidaciones;

/// <summary>
/// Las verificaciones que comparten generar, consultar disponibles y editar (FR-001, FR-002, FR-011,
/// FR-049). <b>La pantalla no es la garantía</b>: se vuelven a hacer al guardar.
/// </summary>
public class ValidadorDeViajes(IRepositorioLiquidaciones liquidaciones, TimeProvider reloj)
{
    /// <summary>
    /// Campos presentes, empresa emisora configurada, transportista externo y activo, y período admitido,
    /// en ese orden (data-model §Generar).
    /// </summary>
    public async Task<(ResultadoLiquidacion? Rechazo, Transportista? Transportista)> ValidarArmadoAsync(
        int? transportistaId,
        int? mes,
        int? anio,
        CancellationToken cancelacion = default)
    {
        if (transportistaId is null or <= 0)
        {
            return (Invalido(MensajesLiquidaciones.TransportistaRequerido, "transportistaId"), null);
        }

        if (mes is null)
        {
            return (Invalido(MensajesLiquidaciones.MesRequerido, "mes"), null);
        }

        if (anio is null)
        {
            return (Invalido(MensajesLiquidaciones.AnioRequerido, "anio"), null);
        }

        var cuitEmisora = await liquidaciones.ObtenerCuitEmpresaEmisoraAsync(cancelacion);

        if (cuitEmisora is null)
        {
            return (ResultadoLiquidacion.Rechazo(
                ErrorLiquidacion.EmpresaEmisoraNoConfigurada,
                MensajesLiquidaciones.EmpresaEmisoraNoConfigurada), null);
        }

        var transportista = await liquidaciones.ObtenerTransportistaAsync(transportistaId.Value, cancelacion);

        if (transportista is null ||
            !ConsultarDetalleLiquidacion.TransportistaLiquidable(transportista, cuitEmisora))
        {
            return (ResultadoLiquidacion.Rechazo(
                ErrorLiquidacion.TransportistaNoLiquidable,
                MensajesLiquidaciones.TransportistaNoLiquidable,
                "transportistaId"), null);
        }

        var hoy = FechaHoyArgentina.Desde(reloj.GetUtcNow());

        if (!ReglasDeLiquidacion.PeriodoValido(mes.Value, anio.Value, hoy))
        {
            return (ResultadoLiquidacion.Rechazo(
                ErrorLiquidacion.PeriodoInvalido,
                MensajesLiquidaciones.PeriodoInvalido(hoy.Year),
                "mes"), null);
        }

        return (null, transportista);
    }

    /// <summary>
    /// Para cada viaje pedido: que exista, esté <c>rendido</c> o <c>facturado</c>, sea del transportista y
    /// del período, y no tenga vínculo vigente con <b>otra</b> liquidación. Devuelve los viajes tal como
    /// están en la base y su total.
    ///
    /// <b>No exige que estén todos los disponibles</b>: la lista enviada manda (FR-008).
    /// </summary>
    /// <param name="liquidacionPropiaId">
    /// Al editar, la liquidación que se edita: sus propios viajes tienen vínculo vigente y no son un
    /// conflicto.
    /// </param>
    public async Task<(ResultadoLiquidacion? Rechazo, IReadOnlyList<Viaje> Viajes, decimal Total)> ValidarViajesAsync(
        int transportistaId,
        int mes,
        int anio,
        IReadOnlyList<int>? viajeIds,
        int? liquidacionPropiaId,
        CancellationToken cancelacion = default)
    {
        var ids = viajeIds?.Distinct().ToList() ?? [];

        if (ids.Count == 0)
        {
            return (ResultadoLiquidacion.Rechazo(
                ErrorLiquidacion.SinViajes,
                MensajesLiquidaciones.SinViajes,
                "viajeIds"), [], 0m);
        }

        var viajes = await liquidaciones.ObtenerViajesAsync(ids, cancelacion);

        if (viajes.Count != ids.Count)
        {
            return (Invalido(MensajesLiquidaciones.ViajeInexistente, "viajeIds"), [], 0m);
        }

        var noLiquidables = viajes
            .OrderBy(viaje => viaje.Fecha)
            .ThenBy(viaje => viaje.Numero)
            .Select(viaje => (Viaje: viaje, Motivo: MotivoDe(viaje, transportistaId, mes, anio)))
            .Where(par => par.Motivo is not null)
            .ToList();

        if (noLiquidables.Count > 0)
        {
            var (primero, motivo) = noLiquidables[0];

            return (new ResultadoLiquidacion(
                ErrorLiquidacion.ViajeNoLiquidable,
                Campo: "viajeIds",
                Mensaje: MensajesLiquidaciones.ViajeNoLiquidable(primero.Numero, motivo!.Value),
                Viajes: [.. noLiquidables.Select(par => new ViajeEnConflictoDeLiquidacion(
                    par.Viaje.Id,
                    par.Viaje.Numero,
                    NombresDeEstadoLiquidacion.EnJson(par.Motivo!.Value),
                    null))]), [], 0m);
        }

        // La consulta previa da el mensaje bueno; el índice cierra la carrera (convención [005]).
        var vinculos = (await liquidaciones.ConsultarVinculosVigentesAsync(ids, cancelacion))
            .Where(vinculo => vinculo.LiquidacionId != liquidacionPropiaId)
            .ToList();

        if (vinculos.Count > 0)
        {
            return (RechazoPorYaLiquidados(vinculos, alEditar: liquidacionPropiaId is not null), [], 0m);
        }

        return (null, viajes, viajes.Sum(viaje => viaje.Importe));
    }

    /// <summary>
    /// <c>409 viaje_ya_liquidado</c> nombrando cada viaje y la liquidación que lo tiene (FR-011). Lo usan
    /// la consulta previa y la traducción de la carrera.
    /// </summary>
    public static ResultadoLiquidacion RechazoPorYaLiquidados(
        IReadOnlyList<VinculoVigente> vinculos,
        bool alEditar)
    {
        var primero = vinculos[0];
        var liquidacion = NumerosVisibles.Liquidacion(primero.LiquidacionNumero);

        return new ResultadoLiquidacion(
            ErrorLiquidacion.ViajeYaLiquidado,
            Campo: "viajeIds",
            Mensaje: alEditar
                ? MensajesLiquidaciones.ViajeYaLiquidadoAlEditar(primero.ViajeNumero, liquidacion)
                : MensajesLiquidaciones.ViajeYaLiquidadoAlGenerar(primero.ViajeNumero, liquidacion),
            Viajes: [.. vinculos.Select(vinculo => new ViajeEnConflictoDeLiquidacion(
                vinculo.ViajeId,
                vinculo.ViajeNumero,
                NombresDeEstadoLiquidacion.EnJson(MotivoViajeNoLiquidable.YaLiquidado),
                NumerosVisibles.Liquidacion(vinculo.LiquidacionNumero)))]);
    }

    /// <summary>
    /// El transportista es el <b>registrado en el viaje</b> al asignarlo, nunca el actual del chofer
    /// (FR-005). El período se mira contra la fecha del viaje.
    /// </summary>
    private static MotivoViajeNoLiquidable? MotivoDe(Viaje viaje, int transportistaId, int mes, int anio)
    {
        if (viaje.Estado is not (EstadoViaje.Rendido or EstadoViaje.Facturado))
        {
            return MotivoViajeNoLiquidable.NoRendidoNiFacturado;
        }

        if (viaje.TransportistaId != transportistaId)
        {
            return MotivoViajeNoLiquidable.DeOtroTransportista;
        }

        if (viaje.Fecha.Month != mes || viaje.Fecha.Year != anio)
        {
            return MotivoViajeNoLiquidable.DeOtroPeriodo;
        }

        return null;
    }

    private static ResultadoLiquidacion Invalido(string mensaje, string campo) =>
        ResultadoLiquidacion.Rechazo(ErrorLiquidacion.DatosInvalidos, mensaje, campo);
}
