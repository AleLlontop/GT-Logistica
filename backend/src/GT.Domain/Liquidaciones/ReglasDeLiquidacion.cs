namespace GT.Domain.Liquidaciones;

/// <summary>
/// Por qué una liquidación no admite una operación (FR-045, FR-053, FR-054).
/// </summary>
public enum MotivoDeBloqueoLiquidacion
{
    Pagada,
    Anulada,
    ConOrdenesDePago,

    /// <summary>Sólo al editar: el transportista se dio de baja o dejó de ser externo (FR-045).</summary>
    TransportistaNoLiquidable,
}

/// <summary>
/// Las reglas puras del módulo.
///
/// <b>Ninguna lee el reloj</b>: las fechas llegan por parámetro, que es lo que permite probar en un test
/// el borde de "hoy" sin esperar a que llegue (convención [005]).
/// </summary>
public static class ReglasDeLiquidacion
{
    /// <summary>
    /// FR-002: mes de 1 a 12 y año de los mismos del Módulo 6, de 2025 al en curso. También el mes en
    /// curso o uno posterior. El año crece con el calendario, por eso vive acá y no en un <c>CHECK</c>.
    /// </summary>
    public static bool PeriodoValido(int mes, int anio, DateOnly hoy) =>
        mes is >= 1 and <= 12 && Facturacion.PeriodoAdmitido.AnioValido(anio, hoy);

    /// <summary>FR-038: entre la fecha de generación y el día en curso, <b>los dos incluidos</b>.</summary>
    public static bool FechaDePagoValida(DateOnly fecha, DateOnly fechaGeneracion, DateOnly hoy) =>
        fecha >= fechaGeneracion && fecha <= hoy;

    /// <summary>FR-027. En una anulada no se muestra, pero la regla no lo decide: lo decide quien muestra.</summary>
    public static decimal RestaPagar(decimal total, decimal pagado) => total - pagado;

    /// <summary>
    /// FR-030, FR-041: <c>pagada</c> cuando el pago deja el saldo exactamente en cero.
    ///
    /// Vive <b>dos veces</b>: acá y como <c>CASE</c> dentro del <c>UPDATE</c> condicional del pago.
    /// <c>CoherenciaDeLiquidacionTests</c> compara las dos sobre el mismo dato (convención [003]).
    /// </summary>
    public static EstadoLiquidacion EstadoTrasPago(decimal total, decimal pagado, decimal importe) =>
        pagado + importe == total ? EstadoLiquidacion.Pagada : EstadoLiquidacion.Pendiente;

    /// <summary>
    /// FR-045: sólo se edita una <c>pendiente</c>, sin órdenes de pago y con su transportista todavía
    /// externo y activo. <c>null</c> significa que se puede.
    /// </summary>
    /// <param name="transportistaLiquidable">
    /// Activo y con un CUIT distinto del de la empresa emisora. Llega calculado: la regla no conoce el
    /// padrón ni la configuración.
    /// </param>
    public static MotivoDeBloqueoLiquidacion? MotivoNoEditable(
        EstadoLiquidacion estado,
        decimal pagado,
        bool transportistaLiquidable) =>
        MotivoNoAnulable(estado, pagado)
        ?? (transportistaLiquidable ? null : MotivoDeBloqueoLiquidacion.TransportistaNoLiquidable);

    /// <summary>
    /// FR-053: sólo se anula una <c>pendiente</c> sin órdenes de pago. El transportista no importa: una
    /// liquidación mal armada de un fletero dado de baja se corrige anulándola.
    /// </summary>
    public static MotivoDeBloqueoLiquidacion? MotivoNoAnulable(EstadoLiquidacion estado, decimal pagado) =>
        estado switch
        {
            EstadoLiquidacion.Pagada => MotivoDeBloqueoLiquidacion.Pagada,
            EstadoLiquidacion.Anulada => MotivoDeBloqueoLiquidacion.Anulada,
            _ when pagado > 0 => MotivoDeBloqueoLiquidacion.ConOrdenesDePago,
            _ => null,
        };
}
