namespace GT.Domain.Pagos;

public enum SituacionVencimientoCheque { Vencido, ProximoAVencer, FueraDelPeriodoDeAlerta }

/// <summary>Situación calculada, independiente de EstadoCheque y sin reloj ni persistencia.</summary>
public static class VencimientoCheque
{
    public static SituacionVencimientoCheque Evaluar(DateOnly vencimiento, DateOnly hoyArgentina) =>
        vencimiento < hoyArgentina ? SituacionVencimientoCheque.Vencido
        : EnAlerta(vencimiento, hoyArgentina) ? SituacionVencimientoCheque.ProximoAVencer
        : SituacionVencimientoCheque.FueraDelPeriodoDeAlerta;
    public static bool EnAlerta(DateOnly vencimiento, DateOnly hoyArgentina) =>
        vencimiento.DayNumber <= hoyArgentina.DayNumber + 30;
}
