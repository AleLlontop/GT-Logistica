namespace GT.Domain.Choferes;

/// <summary>
/// El "hoy" contra el que se calcula el estado de la documentación (FR-017a).
///
/// Es siempre la hora de Argentina (UTC−3), sin importar la zona del servidor ni la del navegador.
/// No es un detalle: el borde declarado en la spec —un documento que vence <b>exactamente hoy</b> es
/// próximo a vencer, y recién pasa a vencido al día siguiente— cambia de resultado según dónde se
/// corte el día.
///
/// Se usa un desplazamiento fijo y no <c>TimeZoneInfo</c> a propósito: Argentina no aplica horario de
/// verano desde 2009, y los identificadores de zona horaria difieren entre Windows y Linux, así que
/// buscarlos por nombre haría que el resultado dependiera del sistema donde corre.
/// </summary>
public static class FechaHoyArgentina
{
    private static readonly TimeSpan Desplazamiento = TimeSpan.FromHours(-3);

    /// <summary>Día en curso en Argentina, derivado del instante UTC recibido.</summary>
    public static DateOnly Desde(DateTimeOffset instanteUtc) =>
        DateOnly.FromDateTime(instanteUtc.ToOffset(Desplazamiento).DateTime);

    /// <summary>Día en curso en Argentina según el reloj del sistema.</summary>
    public static DateOnly Hoy() => Desde(DateTimeOffset.UtcNow);
}
