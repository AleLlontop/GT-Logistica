using GT.Application.Choferes;
using GT.Domain.Caja;

namespace GT.Application.Caja;

// Dentro del espacio de nombres: `GT.Application.Caja` tapa al tipo `Caja` si el alias va arriba.
using CajaEntidad = GT.Domain.Caja.Caja;

/// <summary>
/// Persistencia del módulo. <b>Lee</b> <c>Usuarios</c>, <c>Facturas</c> y <c>OrdenesDePago</c> sin escribirles
/// nada (FR-012).
///
/// Los instantes llegan por parámetro y ninguna consulta lee el reloj (convención [005]). El usuario en sesión
/// llega por parámetro a toda escritura: la caja es personal también en el servidor (FR-035).
/// </summary>
public interface IRepositorioCaja
{
    // ── Consulta de cajas ───────────────────────────────────────────────────────────────────────

    /// <summary>
    /// La caja con su responsable y los totales sumados en SQL, sin rastrear. <paramref name="usuarioId"/>
    /// decide <c>PuedeOperar</c>.
    /// </summary>
    Task<CajaDetalle?> ObtenerDetalleAsync(int id, int usuarioId, CancellationToken cancelacion = default);

    /// <summary>La caja abierta del usuario, o <c>null</c> si no tiene ninguna.</summary>
    Task<CajaDetalle?> ObtenerCajaAbiertaDeAsync(int usuarioId, CancellationToken cancelacion = default);

    /// <summary>Cajas de todos los responsables, por <c>FechaApertura DESC, Id DESC</c> (FR-026).</summary>
    Task<PaginaDe<CajaListado>> ConsultarCajasAsync(int pagina, CancellationToken cancelacion = default);

    /// <summary>
    /// Por qué el usuario no puede operar la caja: no existe, está cerrada o es de otro. <c>null</c> si puede.
    /// Es la relectura de las dos escrituras cuando su <c>UPDATE</c> no afectó ninguna fila.
    /// </summary>
    Task<MotivoNoOperable?> MotivoNoOperableAsync(int id, int usuarioId, CancellationToken cancelacion = default);

    // ── Abrir (data-model §Abrir) ───────────────────────────────────────────────────────────────

    Task<bool> ExisteCajaAbiertaAsync(int usuarioId, CancellationToken cancelacion = default);

    /// <summary>
    /// Inserta la caja. Devuelve su <c>Id</c>.
    /// </summary>
    /// <exception cref="CajaYaAbiertaException">
    /// Si <c>IX_Cajas_UsuarioResponsable_Abierta</c> cortó la inserción: dos aperturas simultáneas (CL3).
    /// </exception>
    Task<int> AbrirAsync(CajaEntidad caja, CancellationToken cancelacion = default);

    // ── Movimientos (data-model §Registrar movimiento) ──────────────────────────────────────────

    /// <summary>
    /// Toma el lock de la caja con un <c>UPDATE</c> que no cambia nada —abierta y del usuario del
    /// movimiento—, revalida la factura con el lock tomado e inserta, todo o nada (research §2).
    /// </summary>
    Task<MovimientoRegistrado> RegistrarMovimientoAsync(
        MovimientoDeCaja movimiento,
        CancellationToken cancelacion = default);

    Task<MovimientoListado?> ObtenerMovimientoAsync(int id, CancellationToken cancelacion = default);

    /// <summary>Los movimientos de una caja, por <c>Fecha DESC, Id DESC</c>.</summary>
    Task<PaginaDe<MovimientoListado>> ConsultarMovimientosDeCajaAsync(
        int cajaId,
        int pagina,
        CancellationToken cancelacion = default);

    /// <summary>Las facturas <c>Pendiente</c>, con el filtro en SQL (research §5).</summary>
    Task<IReadOnlyList<OpcionDeReferencia>> ConsultarFacturasPendientesAsync(CancellationToken cancelacion = default);

    /// <summary>Las 50 órdenes de pago más recientes, sin filtro de estado (research §4).</summary>
    Task<IReadOnlyList<OpcionDeReferencia>> ConsultarOrdenesDePagoAsync(CancellationToken cancelacion = default);

    Task<bool> ExisteOrdenDePagoAsync(int id, CancellationToken cancelacion = default);

    // ── Cierre (data-model §Cerrar) ─────────────────────────────────────────────────────────────

    /// <summary>Una foto, sin transacción: el resumen que se muestra antes de confirmar (FR-016).</summary>
    Task<ResumenDeCierre> ConsultarResumenAsync(int cajaId, CancellationToken cancelacion = default);

    /// <summary>
    /// Toma el lock como el registro de un movimiento, relee los totales con el lock tomado y cierra sólo si
    /// el saldo final coincide con <paramref name="saldoFinalConfirmado"/> (research §2, §3).
    /// </summary>
    Task<CierreIntentado> CerrarAsync(
        int cajaId,
        int usuarioId,
        decimal saldoFinalConfirmado,
        DateTime ocurridoEn,
        CancellationToken cancelacion = default);

    // ── Consulta global (FR-023 a FR-025) ───────────────────────────────────────────────────────

    /// <summary>
    /// Los filtros <b>antes</b> de contar y paginar (research §8). El rango ya llega convertido a instantes
    /// UTC: <paramref name="desde"/> incluido y <paramref name="hastaExcluido"/> excluido.
    /// </summary>
    Task<PaginaDe<MovimientoListado>> ConsultarMovimientosAsync(
        DateTime? desde,
        DateTime? hastaExcluido,
        int? cajaId,
        int pagina,
        CancellationToken cancelacion = default);
}

/// <summary>Por qué quien pide no puede operar una caja.</summary>
public enum MotivoNoOperable
{
    NoEncontrada,
    Cerrada,
    Ajena,
}

/// <summary>El resultado del registro: el <c>Id</c>, o por qué no se registró.</summary>
public record MovimientoRegistrado(int? Id, MotivoNoOperable? NoOperable = null, bool FacturaNoPendiente = false);

/// <summary>El resultado del cierre: cerró, no se podía operar, o el resumen cambió.</summary>
/// <param name="ResumenActual">Con un saldo que no coincide: el resumen releído con el lock tomado.</param>
public record CierreIntentado(bool Cerrada, MotivoNoOperable? NoOperable = null, ResumenDeCierre? ResumenActual = null);

/// <summary>
/// La carrera de doble apertura que la consulta previa no alcanza a cerrar: <c>IX_Cajas_UsuarioResponsable_Abierta</c>
/// corta a la segunda (CL3, convención [003]).
/// </summary>
public class CajaYaAbiertaException(Exception interna)
    : Exception("El usuario ya tiene una caja abierta.", interna);
