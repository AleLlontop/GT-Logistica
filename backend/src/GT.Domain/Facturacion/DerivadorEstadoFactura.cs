namespace GT.Domain.Facturacion;

/// <summary>
/// Cómo se deriva el estado que se ve a partir del que se guarda (FR-041).
///
/// <b>Función pura: recibe la fecha por parámetro y no lee el reloj por dentro</b> (convención [005]).
/// Eso es lo que permite probar en un test lo que a mano exigiría esperar a que venza una factura, y es
/// la cuarta vez que el sistema resuelve así un estado derivable —vencimientos de documentación en los
/// Módulos 3 y 4, <c>demorado</c> en el 5— (research §3).
///
/// <b>Lo que este módulo agrega es que el derivado además se filtra</b> (FR-058a). Por eso la regla está
/// escrita <b>dos veces a propósito</b>: acá, y como predicado dentro de la consulta de
/// <c>RepositorioFacturas</c>, porque filtrar en memoria después de paginar devolvería páginas
/// incompletas. La duplicación la cubre <c>DerivacionVencidaTests</c>, que evalúa las dos sobre el mismo
/// conjunto y compara (convención [003]).
///
/// <b><c>pagada</c> y <c>anulada</c> mandan sobre el vencimiento</b>: una factura cobrada tarde no es
/// una factura vencida, es una factura cobrada.
///
/// <b>El vencimiento del CAE no influye</b>: son dos plazos distintos y sólo el de pago mueve la factura
/// a <c>vencida</c> (FR-041, US5 esc. 10). Por eso esta función no lo recibe.
/// </summary>
public static class DerivadorEstadoFactura
{
    public static EstadoFacturaVisible Derivar(
        EstadoFactura guardado,
        DateOnly vencimientoPago,
        DateOnly hoy) =>
        guardado switch
        {
            EstadoFactura.Pagada => EstadoFacturaVisible.Pagada,
            EstadoFactura.Anulada => EstadoFacturaVisible.Anulada,

            // Estrictamente anterior: una factura que vence **hoy** todavía está en plazo. Con `<=`, la
            // pantalla la mostraría vencida el mismo día en que se puede cobrar sin atraso.
            _ => vencimientoPago < hoy ? EstadoFacturaVisible.Vencida : EstadoFacturaVisible.Pendiente,
        };

    /// <summary>
    /// Días de atraso o de plazo, en días corridos: negativo es atraso (FR-063).
    ///
    /// Es la otra cara de la misma comparación, y por eso vive al lado: si un día cambiara el criterio
    /// de <c>vencida</c>, el panel de vencimientos tiene que cambiar con él.
    /// </summary>
    public static int DiasHasta(DateOnly vencimientoPago, DateOnly hoy) =>
        vencimientoPago.DayNumber - hoy.DayNumber;
}
