namespace GT.Domain.Viajes;

/// <summary>
/// Qué transición de estado se permite (FR-033). Regla pura: no consulta la base ni el reloj.
///
/// <code>
///   (alta) ──▶ pendiente ──▶ en curso ──▶ rendido ⇄ facturado   ← los dos, terminales
///                  │             │
///                  └─────────────┴──▶ anulado                    ← terminal
/// </code>
///
/// Los estados terminales <b>no tienen salida hacia atrás</b>: no hay camino de vuelta a
/// <c>pendiente</c> ni a <c>en curso</c> desde ninguno, y tampoco de <c>rendido</c> a <c>anulado</c>.
/// La única ida y vuelta es <c>rendido ⇄ facturado</c>, y la produce el Módulo 6.
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
    /// <remarks>
    /// <b>⚠ Los dos pares del Módulo 6 no abren ningún camino HTTP nuevo, y conviene saber por qué</b>
    /// (Módulo 6, research §8.2). <see cref="EsTerminal"/> devuelve <c>true</c> para <c>rendido</c> y
    /// para <c>facturado</c>, y los cinco caminos de escritura del Módulo 5 llaman a
    /// <c>EstadoTerminal.Rechazo</c> <b>antes</b> de mirar la transición: un viaje rendido rebota ahí y
    /// nunca llega a consultar este mapa. Además, los tres endpoints de ciclo de vida del Módulo 5
    /// tienen el estado destino fijo en el código —<c>enCurso</c>, <c>rendido</c>, <c>anulado</c>— y
    /// ninguno puede pedir <c>facturado</c>.
    ///
    /// El cambio de estado por facturación lo hace el caso de uso del Módulo 6 y nadie más. Los pares
    /// están acá para que la regla de qué transición es legítima siga viviendo en un solo lugar.
    /// </remarks>
    public static bool EstaPermitida(EstadoViaje actual, EstadoViaje pedido) => (actual, pedido) switch
    {
        (EstadoViaje.Pendiente, EstadoViaje.EnCurso) => true,
        (EstadoViaje.EnCurso, EstadoViaje.Rendido) => true,
        (EstadoViaje.Pendiente, EstadoViaje.Anulado) => true,
        (EstadoViaje.EnCurso, EstadoViaje.Anulado) => true,

        // Módulo 6: al emitir la factura y al anularla (FR-051, FR-047 del Módulo 6).
        (EstadoViaje.Rendido, EstadoViaje.Facturado) => true,
        (EstadoViaje.Facturado, EstadoViaje.Rendido) => true,

        _ => false,
    };

    /// <summary>
    /// <c>true</c> si el viaje ya no admite ninguna escritura: ni edición, ni asignación, ni cambio de
    /// estado. Vale para <b>todos</b> los roles, incluido Administrador del sistema (FR-018, SC-013).
    ///
    /// <c>facturado</c> entra acá por FR-052 del Módulo 6, con el mismo alcance que ya regía para
    /// <c>rendido</c>. Los cinco caminos de escritura del Módulo 5 quedan cerrados <b>sin tocar
    /// ninguno de los cinco</b>, porque los cinco ya consultan esta función.
    /// </summary>
    public static bool EsTerminal(EstadoViaje estado) =>
        estado is EstadoViaje.Rendido or EstadoViaje.Anulado or EstadoViaje.Facturado;
}
