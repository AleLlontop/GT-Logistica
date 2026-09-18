namespace GT.Application.Caja;

// ── Entradas ────────────────────────────────────────────────────────────────────────────────────

/// <summary>Lo que llega al abrir (FR-001, FR-002). Anulable: la validación dice qué falta.</summary>
public record AbrirCajaRequest(decimal? SaldoInicial);

/// <summary>Lo que llega al registrar un movimiento (FR-006 a FR-011). Todo anulable.</summary>
/// <param name="Tipo"><c>ingreso</c> o <c>egreso</c>; cualquier otro valor es inválido.</param>
public record RegistrarMovimientoRequest(
    string? Tipo,
    decimal? Importe,
    string? Concepto,
    int? FacturaId,
    int? OrdenDePagoId);

/// <summary>
/// La confirmación del cierre viaja con el número que confirma (research §3): sin <c>confirmado</c>, o con
/// un saldo distinto del recién calculado, no se cierra nada.
/// </summary>
public record CierreRequest(bool? Confirmado, decimal? SaldoFinalConfirmado);

/// <param name="Desde">Día de Argentina, incluido.</param>
/// <param name="Hasta">Día de Argentina, incluido entero (tasks.md decisión 4).</param>
public record FiltrosDeMovimientos(
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    int? CajaId = null,
    int Pagina = 1);

// ── Salidas ─────────────────────────────────────────────────────────────────────────────────────

/// <summary>Un usuario como lo nombra el módulo: su nombre de usuario vigente.</summary>
public record UsuarioResumen(int Id, string Nombre);

/// <summary>Fila del listado de cajas (FR-026). Cierre y saldo final sólo en las cerradas.</summary>
/// <param name="FechaApertura">Instante UTC con la <c>Z</c> (convención [002]).</param>
public record CajaListado(
    int Id,
    UsuarioResumen Responsable,
    DateTime FechaApertura,
    string Estado,
    DateTime? FechaCierre,
    decimal? SaldoFinal);

/// <summary>
/// Detalle de una caja. Es también la respuesta de la apertura y del cierre, releída (convención [006]).
/// </summary>
/// <param name="SaldoActual">Inicial + ingresos − egresos, calculado al leer (convención [003]).</param>
/// <param name="PuedeOperar">Abierta y del usuario en sesión: lo decide el servidor (tasks.md decisión 2).</param>
public record CajaDetalle(
    int Id,
    UsuarioResumen Responsable,
    DateTime FechaApertura,
    string Estado,
    DateTime? FechaCierre,
    decimal? SaldoFinal,
    decimal SaldoInicial,
    decimal TotalIngresos,
    decimal TotalEgresos,
    decimal SaldoActual,
    bool PuedeOperar);

/// <summary>Fila de cualquier listado de movimientos (CA5).</summary>
/// <param name="Referencia">
/// Armada por el backend —<c>Factura 0001-00000012 · Cliente SA</c> u <c>OP-7</c>— o <c>null</c> (convención
/// [009]).
/// </param>
public record MovimientoListado(
    int Id,
    int CajaId,
    DateTime Fecha,
    string Tipo,
    decimal Importe,
    string Concepto,
    UsuarioResumen Responsable,
    string? Referencia);

/// <summary>
/// Lo que se muestra antes de cerrar (FR-016), y lo que traen los dos <c>409</c> del cierre. Los movimientos
/// van en orden cronológico.
/// </summary>
public record ResumenDeCierre(
    decimal SaldoInicial,
    decimal TotalIngresos,
    decimal TotalEgresos,
    IReadOnlyList<MovimientoListado> Movimientos,
    decimal SaldoFinal);

/// <summary>Una opción de los desplegables de referencia, con el texto armado por el backend.</summary>
public record OpcionDeReferencia(int Id, string Texto);
