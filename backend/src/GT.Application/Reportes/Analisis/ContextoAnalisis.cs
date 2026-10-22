using GT.Domain.Choferes;

namespace GT.Application.Reportes.Analisis;

public enum CriterioTemporalAnalisis
{
    FechaViaje,
    FechaFactura,
    FechaCobro,
    PeriodoLiquidado,
    FechaPago,
    FechaMovimientoArgentina,
    SituacionActual,
    PerspectivasSeparadas,
}

/// <summary>Un reloj capturado por respuesta, compartido por consultas, estados derivados y metadatos.</summary>
public sealed record ContextoAnalisis(
    DateOnly? Desde,
    DateOnly? Hasta,
    DateTimeOffset ConsultadoEn,
    DateOnly HoyArgentina,
    CriterioTemporalAnalisis CriterioTemporal,
    string? PeriodoLiquidadoDesde = null,
    string? PeriodoLiquidadoHasta = null)
{
    public static ContextoAnalisis Capturar(TimeProvider reloj, PeriodoAnalisis? periodo, CriterioTemporalAnalisis criterio)
        => DesdeInstante(reloj.GetUtcNow(), periodo, criterio);

    public static ContextoAnalisis DesdeInstante(DateTimeOffset instante, PeriodoAnalisis? periodo, CriterioTemporalAnalisis criterio)
    {
        if (!Enum.IsDefined(criterio)) throw new ArgumentOutOfRangeException(nameof(criterio));
        if (criterio != CriterioTemporalAnalisis.SituacionActual && periodo is null)
            throw new ArgumentException("El análisis requiere un período.", nameof(periodo));
        if (criterio == CriterioTemporalAnalisis.SituacionActual && periodo is not null)
            throw new ArgumentException("Los vencimientos muestran la situación actual, sin período histórico.", nameof(periodo));
        var esLiquidado = criterio == CriterioTemporalAnalisis.PeriodoLiquidado;
        return new(periodo?.Desde, periodo?.Hasta, instante.ToUniversalTime(), FechaHoyArgentina.Desde(instante), criterio,
            esLiquidado ? periodo!.MesLiquidadoDesde.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture) : null,
            esLiquidado ? periodo!.MesLiquidadoHasta.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture) : null);
    }
}
