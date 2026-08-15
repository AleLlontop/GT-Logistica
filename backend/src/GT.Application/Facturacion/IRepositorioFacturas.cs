using GT.Application.Choferes;
using GT.Domain.Facturacion;
using GT.Domain.Viajes;

namespace GT.Application.Facturacion;

/// <summary>
/// Los cinco filtros del listado, combinables entre sí (FR-058).
/// </summary>
/// <param name="Estado">
/// <c>null</c> significa <b>todas, incluidas las anuladas</b>, y la pantalla lo dice explícitamente
/// (FR-064). Es al revés que el listado de viajes, donde omitir el filtro escondía los anulados: acá
/// una factura anulada sigue siendo parte de la historia de cobranza del cliente.
///
/// Opera sobre el estado <b>derivado</b>, y sus cuatro valores son excluyentes (FR-058a).
/// </param>
public record FiltrosDeFacturas(
    int? ClienteId = null,
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    int? Mes = null,
    int? Anio = null,
    EstadoFacturaVisible? Estado = null,
    TipoComprobante? TipoComprobante = null,
    int Pagina = 1);

/// <summary>
/// Un viaje que la emisión no pudo tomar, con la factura que ya lo tiene si la hay (FR-053).
/// </summary>
/// <param name="NumeroDeFactura">
/// El comprobante que lo incluye, o <c>null</c> si el viaje dejó de estar <c>rendido</c> por otro
/// motivo. El rechazo lo nombra: saber que el viaje no está disponible sin saber dónde quedó no ayuda
/// a resolverlo (convención [004]).
/// </param>
public record ViajeTomado(int Id, int Numero, string? NumeroDeFactura);

/// <summary>
/// Lo que devuelve la transacción de emisión: pasó todo, o no pasó nada y estos viajes lo impidieron.
/// </summary>
public record ResultadoDeEmision(bool Exitoso, IReadOnlyList<ViajeTomado> ViajesTomados)
{
    public static readonly ResultadoDeEmision Ok = new(true, []);
}

public interface IRepositorioFacturas
{
    // ── Armado ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Los viajes que se pueden ofrecer para facturar: del cliente, en estado <c>rendido</c>, con
    /// fecha dentro del mes y año elegidos y <b>sin factura vigente</b> (FR-015 a FR-018).
    ///
    /// <b>Los que no tienen remito vienen igual.</b> No se esconden: quien opera tiene que ver por qué
    /// no puede facturarlos, y un listado no oculta filas en silencio (FR-019a, convención [003]).
    /// </summary>
    Task<IReadOnlyList<Viaje>> ConsultarFacturablesAsync(
        int clienteId,
        int mes,
        int anio,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Los viajes elegidos, tal como están <b>en la base</b> y no como los describió el cuerpo de la
    /// petición. De acá salen los importes con los que se calcula el neto: FR-024 exige que no lleguen
    /// nunca desde el cliente HTTP.
    /// </summary>
    Task<IReadOnlyList<Viaje>> ObtenerViajesAsync(
        IReadOnlyList<int> ids,
        CancellationToken cancelacion = default);

    Task<Cliente?> ObtenerClienteAsync(int clienteId, CancellationToken cancelacion = default);

    /// <summary>
    /// La factura <b>no anulada</b> que ya usa ese número, o <c>null</c> si está libre. Es lo que
    /// permite que el rechazo la nombre con su fecha y su cliente (FR-027).
    ///
    /// La consulta previa da el mensaje bueno; el índice único filtrado cierra la carrera.
    /// </summary>
    Task<FacturaCliente?> ObtenerPorNumeroAsync(
        string numeroComprobante,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Las anuladas de ese cliente que todavía nadie refacturó, para el desplegable de la
    /// Refacturación (FR-049, FR-049a).
    /// </summary>
    Task<IReadOnlyList<FacturaCliente>> ConsultarAnuladasSinReemplazoAsync(
        int clienteId,
        CancellationToken cancelacion = default);

    // ── Consulta ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// El listado paginado, con los cinco filtros y el estado derivado resueltos <b>dentro de la
    /// consulta</b> (FR-057 a FR-059).
    /// </summary>
    /// <param name="hoy">
    /// El día contra el que se deriva <c>vencida</c>. Llega por parámetro y no se lee del reloj adentro
    /// de la consulta, que es lo que permite fijarlo en un test en vez de esperar a que venza una
    /// factura (convención [005]).
    /// </param>
    Task<PaginaDe<FacturaListado>> ConsultarAsync(
        FiltrosDeFacturas filtros,
        DateOnly hoy,
        CancellationToken cancelacion = default);

    /// <summary>
    /// La factura con todo lo que la ficha necesita: cliente, viajes incluidos, historial con el
    /// usuario de cada línea y la factura reemplazada (FR-060).
    /// </summary>
    Task<FacturaCliente?> ObtenerFichaAsync(int id, CancellationToken cancelacion = default);

    /// <summary>Seguida por el contexto, con sus viajes cargados: lo devuelto se modifica.</summary>
    Task<FacturaCliente?> ObtenerParaModificarAsync(int id, CancellationToken cancelacion = default);

    /// <summary>
    /// La Refacturación que reemplazó a esta factura anulada, o <c>null</c>.
    ///
    /// <b>Se resuelve por consulta y no por una columna espejo</b> que habría que mantener
    /// sincronizada y que podría discrepar del dato que ya está (FR-050).
    /// </summary>
    Task<FacturaCliente?> ObtenerQueLaReemplazaAsync(int id, CancellationToken cancelacion = default);

    // ── Las tres operaciones que tienen que ser atómicas ────────────────────────────────────────

    /// <summary>
    /// La transacción de emisión de data-model §Emitir (FR-054, SC-005).
    ///
    /// El documento <b>ya está escrito en disco</b> y su ruta viene puesta en la factura: no necesita
    /// el <c>Id</c>, porque el número de comprobante lo tipea el usuario, así que nada obliga a
    /// escribirlo después (research §6). Si esto falla, quien llama borra el archivo.
    ///
    /// Adentro: inserta la factura, su entrada de historial, marca los viajes con un <b><c>UPDATE</c>
    /// condicional cuyo número de filas afectadas se verifica</b> —<c>Estado = rendido AND FacturaId
    /// IS NULL</c>— y escribe una línea de <c>CambioDeEstadoViaje</c> por viaje. Si las filas
    /// afectadas no son las esperadas, deshace todo y devuelve los viajes que lo impidieron.
    ///
    /// Ese <c>UPDATE</c> es lo que cierra la carrera entre dos operadores simultáneos: la segunda
    /// transacción se bloquea sobre la fila que la primera está modificando y, al desbloquearse,
    /// reevalúa el <c>WHERE</c> contra el dato ya comprometido (research §4).
    /// </summary>
    Task<ResultadoDeEmision> EmitirAsync(
        FacturaCliente factura,
        IReadOnlyList<int> viajeIds,
        int usuarioId,
        DateTime ocurridoEn,
        CancellationToken cancelacion = default);

    /// <summary>
    /// La transacción de corrección de data-model §Corregir (FR-035, FR-031b).
    ///
    /// La factura llega con los cuatro campos ya modificados. Acá se escribe el PDF nuevo, se agrega
    /// la entrada de corrección al historial, se confirma, y <b>recién después</b> se borra el
    /// anterior: nunca se sobreescribe en el lugar, porque una falla a mitad de escritura dejaría un
    /// PDF corrupto donde antes había uno bueno (research §6).
    /// </summary>
    /// <param name="escribirDocumento">
    /// Arma y escribe el documento nuevo, y devuelve su ruta. Llega como parámetro porque el armador
    /// vive en la capa de aplicación y la transacción acá: es lo que permite que la escritura pase
    /// <b>dentro</b> sin que el repositorio conozca QuestPDF.
    /// </param>
    Task CorregirAsync(
        FacturaCliente factura,
        int usuarioId,
        DateTime ocurridoEn,
        Func<FacturaCliente, CancellationToken, Task<string>> escribirDocumento,
        CancellationToken cancelacion = default);

    /// <summary>
    /// La transacción de anulación de data-model §Anular (FR-046 a FR-048, FR-031b).
    ///
    /// Cambia el estado, escribe el historial, devuelve <b>todos</b> los viajes a <c>rendido</c> con
    /// su <c>FacturaId</c> en nulo, escribe una línea de <c>CambioDeEstadoViaje</c> por viaje, y
    /// <b>regenera el documento adentro de la misma transacción</b>: si no se puede armar, la
    /// anulación no queda aplicada a medias y los viajes no vuelven a <c>rendido</c> (FR-031b).
    ///
    /// El PDF anterior se borra recién después de confirmar.
    /// </summary>
    Task AnularAsync(
        FacturaCliente factura,
        string motivo,
        int usuarioId,
        DateTime ocurridoEn,
        Func<FacturaCliente, CancellationToken, Task<string>> escribirDocumento,
        CancellationToken cancelacion = default);

    /// <summary>
    /// El cobro y su línea de historial, en un solo <c>SaveChanges</c> —y por lo tanto en una sola
    /// transacción— (FR-042).
    ///
    /// <b>No regenera el documento</b>: la fecha de cobro no sale impresa, y cobrar es el único cambio
    /// de estado que no lo regenera. Las operaciones que regeneran son exactamente tres: emitir,
    /// corregir y anular (spec §Clarifications, CHK027).
    /// </summary>
    Task RegistrarCobroAsync(
        FacturaCliente factura,
        DateOnly fechaCobro,
        int usuarioId,
        DateTime ocurridoEn,
        CancellationToken cancelacion = default);

    // ── Reportes ────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Las <c>vencida</c> y las que vencen dentro de los 7 días corridos siguientes; las
    /// <c>pagada</c> y <c>anulada</c> no figuran (FR-063). Los días de atraso o de plazo se calculan
    /// en la consulta.
    /// </summary>
    Task<IReadOnlyList<FilaDeVencimiento>> ConsultarVencimientosAsync(
        DateOnly hoy,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Facturado, cobrado y pendiente por cliente, <b>agregados dentro de la consulta SQL</b>
    /// (FR-061).
    ///
    /// La exclusión de las anuladas va escrita como predicado, no como filtrado posterior: escrita una
    /// sola vez no puede diferir entre una columna y otra, ni entre estas y el listado. Eso es lo que
    /// sostiene SC-011 (FR-062).
    /// </summary>
    Task<IReadOnlyList<TotalPorCliente>> ConsultarTotalesAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancelacion = default);
}

/// <summary>
/// La carrera por el número de comprobante que la consulta previa no alcanza a cerrar: dos emisiones
/// simultáneas con el mismo número pasan las dos la verificación y el índice único filtrado corta la
/// segunda (convención [003], FR-027).
/// </summary>
public class NumeroDuplicadoException(Exception interna)
    : Exception("El número de comprobante ya lo usa otra factura vigente.", interna);

/// <summary>
/// La carrera por la refacturación: dos operadores emiten a la vez sendas Refacturaciones que
/// reemplazan a la misma factura anulada. El índice único filtrado corta a la segunda (FR-049a).
/// </summary>
public class AnuladaYaReemplazadaException(Exception interna)
    : Exception("Esa factura anulada ya fue reemplazada por otra Refacturación.", interna);
