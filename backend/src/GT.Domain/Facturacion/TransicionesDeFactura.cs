namespace GT.Domain.Facturacion;

/// <summary>
/// Qué cambio de estado admite una factura (FR-043). Regla pura: no consulta la base ni el reloj.
///
/// <code>
///   (emisión) ──▶ pendiente ⇄ vencida ──▶ pagada    ← terminal
///                     │           │
///                     └───────────┴──▶ anulada      ← terminal
/// </code>
///
/// <c>pendiente</c> y <c>vencida</c> <b>no son dos estados guardados</b>: son el mismo, derivado contra
/// el día en curso. Están dibujados así porque desde los dos se llega a lo mismo.
///
/// <b>Los dos estados terminales no tienen salida.</b> No existe ninguna acción que revierta un cobro ni
/// que devuelva una anulada a <c>pendiente</c> (FR-043, FR-038). No están ocultas: no existen.
///
/// Cada transición es un recurso propio del API y nunca un campo del <c>PUT</c> (FR-044): así corregir un
/// CAE no puede cobrar ni anular una factura en silencio.
/// </summary>
public static class TransicionesDeFactura
{
    /// <summary>
    /// Un salto que la pantalla no ofrece pero el endpoint igual verifica: cobrar una factura ya pagada,
    /// o anular una anulada, se rechazan aunque se invoquen a mano.
    /// </summary>
    public static bool EstaPermitida(EstadoFactura actual, EstadoFactura pedido) =>
        (actual, pedido) switch
        {
            (EstadoFactura.Pendiente, EstadoFactura.Pagada) => true,
            (EstadoFactura.Pendiente, EstadoFactura.Anulada) => true,
            _ => false,
        };

    /// <summary>
    /// <c>true</c> si la factura ya no admite ningún cambio de estado. Vale para <b>todos</b> los roles,
    /// incluido Administrador del sistema (FR-043).
    ///
    /// <b>No implica que sea inmutable del todo</b>: una factura <c>pagada</c> se puede corregir, y eso es
    /// lo correcto —corregir un CAE mal tipeado no le toca el estado ni la fecha de cobro (FR-035,
    /// US4 esc. 8)—. La única inmutable de verdad es la <c>anulada</c> (FR-038).
    /// </summary>
    public static bool EsTerminal(EstadoFactura estado) =>
        estado is EstadoFactura.Pagada or EstadoFactura.Anulada;
}
