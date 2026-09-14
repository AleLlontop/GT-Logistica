using GT.Application.Choferes;
using GT.Domain.Choferes;
using GT.Domain.Liquidaciones;
using GT.Domain.Viajes;

namespace GT.Application.Liquidaciones;

/// <summary>Un viaje con vínculo vigente, y la liquidación que lo tiene.</summary>
public record VinculoVigente(int ViajeId, int ViajeNumero, int LiquidacionId, int LiquidacionNumero);

/// <summary>La orden recién registrada y el estado en que dejó a la liquidación.</summary>
public record OrdenRegistrada(int Numero, EstadoLiquidacion EstadoResultante);

/// <summary>
/// Persistencia del módulo. <b>Lee</b> <c>Transportistas</c>, <c>Viajes</c> y <c>EmpresaEmisora</c> sin
/// escribirles nada (research §5).
///
/// Los instantes llegan por parámetro y ninguna consulta lee el reloj (convención [005]).
/// </summary>
public interface IRepositorioLiquidaciones
{
    // ── Armado ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>El CUIT de la empresa emisora, o <c>null</c> si todavía no se configuró (FR-001a).</summary>
    Task<string?> ObtenerCuitEmpresaEmisoraAsync(CancellationToken cancelacion = default);

    Task<Transportista?> ObtenerTransportistaAsync(int id, CancellationToken cancelacion = default);

    /// <summary>
    /// Los transportistas cuyo CUIT no es el de la empresa emisora, por razón social (research §5). Sin
    /// <paramref name="incluirInactivos"/>, sólo los activos.
    /// </summary>
    Task<IReadOnlyList<Transportista>> ConsultarTransportistasLiquidablesAsync(
        string cuitEmpresaEmisora,
        bool incluirInactivos,
        CancellationToken cancelacion = default);

    /// <summary>
    /// FR-004: del transportista <b>registrado en el viaje</b>, con fecha en el período, <c>rendido</c> o
    /// <c>facturado</c>, y sin vínculo vigente. Ordenados por fecha y número.
    /// </summary>
    Task<IReadOnlyList<Viaje>> ConsultarDisponiblesAsync(
        int transportistaId,
        int mes,
        int anio,
        CancellationToken cancelacion = default);

    /// <summary>Los viajes pedidos, tal como están en la base y sin rastrear: de acá sale el total.</summary>
    Task<IReadOnlyList<Viaje>> ObtenerViajesAsync(
        IReadOnlyCollection<int> ids,
        CancellationToken cancelacion = default);

    /// <summary>Cuáles de esos viajes ya tienen vínculo vigente, y en qué liquidación.</summary>
    Task<IReadOnlyList<VinculoVigente>> ConsultarVinculosVigentesAsync(
        IReadOnlyCollection<int> viajeIds,
        CancellationToken cancelacion = default);

    // ── Consulta ────────────────────────────────────────────────────────────────────────────────

    /// <summary>El listado con los cuatro filtros aplicados <b>antes</b> de paginar (FR-022, FR-023).</summary>
    Task<PaginaDe<LiquidacionListado>> ConsultarAsync(
        FiltrosDeLiquidaciones filtros,
        CancellationToken cancelacion = default);

    /// <summary>
    /// La liquidación con su transportista, <b>todos</b> sus vínculos —vigentes o no— con sus viajes, sus
    /// órdenes de pago con el usuario y su historial con el usuario y los viajes de cada edición.
    /// </summary>
    Task<Liquidacion?> ObtenerDetalleAsync(int id, CancellationToken cancelacion = default);

    // ── Las cuatro transacciones (data-model §Transacciones) ────────────────────────────────────

    /// <summary>
    /// Inserta la liquidación, sus vínculos y la entrada <c>generacion</c>, todo o nada (FR-018).
    /// Devuelve el <c>Id</c> creado.
    /// </summary>
    /// <exception cref="ViajeYaLiquidadoException">
    /// Otro operador se adelantó con alguno de los viajes: el índice filtrado cortó la inserción y la
    /// transacción ya se deshizo.
    /// </exception>
    Task<int> GenerarAsync(
        Liquidacion liquidacion,
        IReadOnlyList<int> viajeIds,
        int usuarioId,
        DateTime ocurridoEn,
        CancellationToken cancelacion = default);

    /// <summary>
    /// El <c>UPDATE</c> condicional <b>primero</b> —pendiente, sin pagos y en la versión abierta—, después
    /// la diferencia de viajes y la entrada <c>edicion</c> con sus viajes. <c>false</c> si el
    /// <c>UPDATE</c> no afectó ninguna fila: no se tocó nada.
    /// </summary>
    /// <exception cref="ViajeYaLiquidadoException">Un agregado ya lo tomó otra liquidación.</exception>
    Task<bool> EditarAsync(
        int id,
        int versionAbierta,
        decimal nuevoTotal,
        IReadOnlyList<int> quitados,
        IReadOnlyList<int> agregados,
        int usuarioId,
        DateTime ocurridoEn,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Estado y motivo con <c>UPDATE</c> condicional, todos los vínculos a <c>Vigente = 0</c> y la entrada
    /// <c>anulacion</c>. <c>false</c> si ya no estaba pendiente y sin pagos.
    /// </summary>
    Task<bool> AnularAsync(
        int id,
        string motivo,
        int usuarioId,
        DateTime ocurridoEn,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Suma el importe y deja el estado con un <c>UPDATE</c> condicional, inserta la orden y, si la dejó
    /// pagada, la entrada <c>pagada</c>. <c>null</c> si el <c>UPDATE</c> no afectó ninguna fila.
    /// </summary>
    Task<OrdenRegistrada?> RegistrarPagoAsync(
        int id,
        DateOnly fechaPago,
        decimal importe,
        int usuarioId,
        DateTime registradaEn,
        CancellationToken cancelacion = default);
}

/// <summary>
/// La carrera por un viaje que la consulta previa no alcanza a cerrar: dos operadores lo pasan los dos y
/// <c>IX_LiquidacionViajes_ViajeVigente</c> corta al segundo (FR-014, convención [003]).
/// </summary>
public class ViajeYaLiquidadoException(Exception interna)
    : Exception("El viaje ya está en otra liquidación vigente.", interna);
