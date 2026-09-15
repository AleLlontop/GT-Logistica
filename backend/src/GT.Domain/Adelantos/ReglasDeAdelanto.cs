namespace GT.Domain.Adelantos;

/// <summary>
/// Las reglas puras del módulo.
///
/// <b>Ninguna lee el reloj</b>: el día llega por parámetro, que es lo que permite probar el piso del
/// 1 de enero sin esperar a enero (convención [005]).
/// </summary>
public static class ReglasDeAdelanto
{
    /// <summary>
    /// El mayor importe que entra en <c>decimal(18,2)</c>. Uno más grande se rechaza como formato
    /// incorrecto y no llega a la base como un <c>500</c> (research §9.8).
    /// </summary>
    public const decimal ImporteMaximo = 9_999_999_999_999_999.99m;

    /// <summary>
    /// FR-005: el primer día del mes anterior a <paramref name="hoy"/>, cruzando el año.
    ///
    /// Se construye con <c>AddMonths(-1)</c> sobre el primero del mes, <b>nunca restando uno al mes a
    /// mano</b>: en enero eso daría el mes cero (research §9.9). Vive dos veces —acá y en
    /// <c>servicioAdelantos.ts</c>—, y el servidor es quien la garantiza (convención [009]).
    /// </summary>
    public static DateOnly PrimeraFechaAdmitida(DateOnly hoy) =>
        new DateOnly(hoy.Year, hoy.Month, 1).AddMonths(-1);

    /// <summary>FR-005: entre el piso y hoy, <b>los dos incluidos</b>.</summary>
    public static bool FechaAdmitida(DateOnly fecha, DateOnly hoy) =>
        fecha >= PrimeraFechaAdmitida(hoy) && fecha <= hoy;

    /// <summary>FR-007, FR-008: mayor que cero, a lo sumo dos decimales y dentro de <c>decimal(18,2)</c>.</summary>
    public static bool ImporteValido(decimal importe) =>
        importe > 0m && decimal.Round(importe, 2) == importe && importe <= ImporteMaximo;

    /// <summary>
    /// FR-021: sólo se aprueba o rechaza un pendiente. <c>null</c> significa que se puede; si no, el
    /// estado en que está, que es lo que el rechazo informa.
    /// </summary>
    public static EstadoAdelanto? MotivoNoResoluble(EstadoAdelanto estado) =>
        estado is EstadoAdelanto.Pendiente ? null : estado;

    /// <summary>
    /// FR-027, FR-028: sólo se anula un aprobado, y en esta versión ninguna otra condición lo impide.
    /// </summary>
    public static EstadoAdelanto? MotivoNoAnulable(EstadoAdelanto estado) =>
        estado is EstadoAdelanto.Aprobado ? null : estado;
}
