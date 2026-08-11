namespace GT.Domain.Viajes;

/// <summary>
/// Qué transición de estado se permite (FR-033). Regla pura: no consulta la base ni el reloj.
///
/// <code>
///   (alta) ──▶ pendiente ──▶ en curso ──▶ rendido   ← terminal e inmutable
///                  │             │
///                  └─────────────┴──▶ anulado       ← terminal
/// </code>
///
/// Los dos estados terminales <b>no tienen salida</b>: no hay camino de vuelta a <c>pendiente</c> ni a
/// <c>en curso</c> desde ninguno de los dos, y tampoco de <c>rendido</c> a <c>anulado</c>.
///
/// Cada transición es un recurso propio del API y nunca un campo del <c>PUT</c> (FR-034): así corregir
/// un destino no puede avanzar ni anular un viaje en silencio.
/// </summary>
public static class TransicionesDeViaje
{
    /// <summary>
    /// Un salto que la pantalla no ofrece pero el endpoint igual verifica: <c>pendiente → rendido</c>
    /// se rechaza aunque se invoque a mano (US4 esc. 10).
    /// </summary>
    public static bool EstaPermitida(EstadoViaje actual, EstadoViaje pedido) => (actual, pedido) switch
    {
        (EstadoViaje.Pendiente, EstadoViaje.EnCurso) => true,
        (EstadoViaje.EnCurso, EstadoViaje.Rendido) => true,
        (EstadoViaje.Pendiente, EstadoViaje.Anulado) => true,
        (EstadoViaje.EnCurso, EstadoViaje.Anulado) => true,
        _ => false,
    };

    /// <summary>
    /// <c>true</c> si el viaje ya no admite ninguna escritura: ni edición, ni asignación, ni cambio de
    /// estado. Vale para <b>todos</b> los roles, incluido Administrador del sistema (FR-018, SC-013).
    /// </summary>
    public static bool EsTerminal(EstadoViaje estado) =>
        estado is EstadoViaje.Rendido or EstadoViaje.Anulado;
}
