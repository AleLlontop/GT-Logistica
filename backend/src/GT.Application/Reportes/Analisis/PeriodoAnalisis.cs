using GT.Domain.Choferes;

namespace GT.Application.Reportes.Analisis;

/// <summary>Período inclusivo de negocio; los flujos y documentos eligen su propio criterio temporal.</summary>
public sealed record PeriodoAnalisis
{
    public DateOnly Desde { get; }
    public DateOnly Hasta { get; }
    public DateOnly MesLiquidadoDesde => new(Desde.Year, Desde.Month, 1);
    public DateOnly MesLiquidadoHasta => new(Hasta.Year, Hasta.Month, 1);

    public PeriodoAnalisis(DateOnly desde, DateOnly hasta)
    {
        if (desde > hasta)
            throw new ArgumentException("Desde no puede ser posterior a Hasta.", nameof(desde));
        Desde = desde;
        Hasta = hasta;
    }

    public static PeriodoAnalisis Requerido(DateOnly? desde, DateOnly? hasta)
    {
        if (desde is null || hasta is null)
            throw new ArgumentException("Elegí las fechas desde y hasta del análisis.");
        return new(desde.Value, hasta.Value);
    }

    public static PeriodoAnalisis MesActual(DateTimeOffset instante)
    {
        var hoy = FechaHoyArgentina.Desde(instante);
        return new(new(hoy.Year, hoy.Month, 1), new(hoy.Year, hoy.Month, DateTime.DaysInMonth(hoy.Year, hoy.Month)));
    }

    public bool Incluye(DateOnly fecha) => fecha >= Desde && fecha <= Hasta;
    public bool IncluyeMes(int anio, int mes)
    {
        var fecha = new DateOnly(anio, mes, 1);
        return fecha >= MesLiquidadoDesde && fecha <= MesLiquidadoHasta;
    }

    /// <summary>Intervalo UTC semiabierto de días argentinos, para instantes como movimientos de Caja.</summary>
    public IntervaloUtcAnalisis ParaInstantesArgentina()
    {
        // El día siguiente a MaxValue no existe en DateTime/SQL datetime2; no envolver ni recortar el día.
        if (Hasta == DateOnly.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(Hasta), "La fecha hasta no permite representar el fin del día argentino.");
        static DateTime MedianocheUtc(DateOnly dia) =>
            new DateTimeOffset(dia.ToDateTime(TimeOnly.MinValue), TimeSpan.FromHours(-3)).UtcDateTime;
        return new(MedianocheUtc(Desde), MedianocheUtc(Hasta.AddDays(1)));
    }
}

public sealed record IntervaloUtcAnalisis(DateTime DesdeInclusivo, DateTime HastaExclusivo);
