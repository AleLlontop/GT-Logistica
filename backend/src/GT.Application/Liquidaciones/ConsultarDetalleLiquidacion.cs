using GT.Domain.Choferes;
using GT.Domain.Liquidaciones;

namespace GT.Application.Liquidaciones;

/// <summary>
/// Detalle de una liquidación (FR-025). <b>Es también la relectura de las cuatro escrituras</b>: la
/// entidad con la que se escribió no refleja lo que hicieron los <c>UPDATE</c> condicionales
/// (convención [006], research §12.4).
/// </summary>
public class ConsultarDetalleLiquidacion(IRepositorioLiquidaciones liquidaciones)
{
    public async Task<LiquidacionDetalle?> EjecutarAsync(int id, CancellationToken cancelacion = default)
    {
        var liquidacion = await liquidaciones.ObtenerDetalleAsync(id, cancelacion);

        if (liquidacion is null)
        {
            return null;
        }

        var cuitEmisora = await liquidaciones.ObtenerCuitEmpresaEmisoraAsync(cancelacion);

        return Armar(liquidacion, cuitEmisora);
    }

    /// <summary>
    /// Externo y activo (FR-001): el mismo criterio para ofrecerlo al generar y para dejar editar sus
    /// liquidaciones (FR-045). Sin empresa emisora no hay externo posible.
    /// </summary>
    public static bool TransportistaLiquidable(Transportista transportista, string? cuitEmisora) =>
        transportista.Activo && cuitEmisora is not null && transportista.Cuit != cuitEmisora;

    /// <summary>
    /// La fecha de Argentina del instante de la entrada <c>generacion</c> (research §12.8): una liquidación
    /// generada a las 22 del 31/07 es del 31/07 aunque en UTC ya sea 01/08.
    /// </summary>
    public static DateOnly FechaDeGeneracion(Liquidacion liquidacion)
    {
        var instante = liquidacion.Cambios
            .First(cambio => cambio.Operacion == OperacionDeLiquidacion.Generacion)
            .OcurridoEn;

        return FechaHoyArgentina.Desde(new DateTimeOffset(DateTime.SpecifyKind(instante, DateTimeKind.Utc)));
    }

    public static LiquidacionDetalle Armar(Liquidacion liquidacion, string? cuitEmisora)
    {
        var transportista = liquidacion.Transportista!;
        var anulada = liquidacion.Estado is EstadoLiquidacion.Anulada;
        var numero = NumerosVisibles.Liquidacion(liquidacion.Numero);

        return new LiquidacionDetalle(
            liquidacion.Id,
            numero,
            liquidacion.PeriodoMes,
            liquidacion.PeriodoAnio,
            FechaDeGeneracion(liquidacion).ToString("yyyy-MM-dd"),

            // Del padrón, no copiado: una razón social corregida después se ve acá (FR-026).
            new TransportistaResumen(transportista.Id, transportista.Nombre, transportista.Cuit, transportista.Activo),

            NombresDeEstadoLiquidacion.EnJson(liquidacion.Estado),
            liquidacion.MotivoAnulacion,
            liquidacion.ImporteTotal,
            liquidacion.ImportePagado,
            anulada ? null : ReglasDeLiquidacion.RestaPagar(liquidacion.ImporteTotal, liquidacion.ImportePagado),
            liquidacion.Version,

            // Una anulada sigue mostrando los viajes que agrupaba (FR-028); una vigente, sólo los vigentes.
            [.. liquidacion.Viajes
                .Where(vinculo => anulada || vinculo.Vigente)
                .Select(vinculo => vinculo.Viaje!)
                .OrderBy(viaje => viaje.Fecha)
                .ThenBy(viaje => viaje.Numero)
                .Select(viaje => new ViajeLiquidado(
                    viaje.Id,
                    viaje.Numero,
                    viaje.Fecha.ToString("yyyy-MM-dd"),
                    viaje.Origen,
                    viaje.Destino,
                    viaje.Importe))],

            [.. liquidacion.OrdenesDePago
                .OrderBy(orden => orden.FechaPago)
                .ThenBy(orden => orden.Numero)
                .Select(orden => new OrdenDePagoDetalle(
                    orden.Id,
                    NumerosVisibles.OrdenDePago(orden.Numero),
                    orden.FechaPago.ToString("yyyy-MM-dd"),
                    orden.Importe,
                    orden.Usuario?.Username ?? $"Usuario {orden.UsuarioId}",
                    orden.RegistradaEn))],

            // De la más vieja a la más nueva (FR-033).
            [.. liquidacion.Cambios
                .OrderBy(cambio => cambio.OcurridoEn)
                .ThenBy(cambio => cambio.Id)
                .Select(cambio => new EntradaDeHistorialLiquidacion(
                    NombresDeEstadoLiquidacion.EnJson(cambio.Operacion),
                    cambio.Usuario?.Username ?? $"Usuario {cambio.UsuarioId}",
                    cambio.OcurridoEn,
                    NumerosDe(cambio, agregado: false),
                    NumerosDe(cambio, agregado: true)))],

            ReglasDeLiquidacion.MotivoNoEditable(
                liquidacion.Estado,
                liquidacion.ImportePagado,
                TransportistaLiquidable(transportista, cuitEmisora)) is null,
            ReglasDeLiquidacion.MotivoNoAnulable(liquidacion.Estado, liquidacion.ImportePagado) is null,
            liquidacion.Estado is EstadoLiquidacion.Pendiente);
    }

    private static IReadOnlyList<int> NumerosDe(CambioDeLiquidacion cambio, bool agregado) =>
        [.. cambio.Viajes
            .Where(viaje => viaje.Agregado == agregado)
            .Select(viaje => viaje.Viaje!.Numero)
            .Order()];
}
