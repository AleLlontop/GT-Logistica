using GT.Application.Choferes;
using GT.Domain.Viajes;

namespace GT.Application.Viajes;

/// <summary>
/// Los cuatro filtros del listado más la búsqueda, combinables entre sí (FR-041, FR-042).
/// </summary>
/// <param name="Estado">
/// <c>null</c> significa <b>todos menos los anulados</b>, no "todos": la exclusión es un predicado de
/// la consulta y la pantalla dice explícitamente qué está mostrando (FR-044, FR-049).
/// </param>
/// <param name="TransportistaId">
/// Compara contra el transportista <b>registrado en el viaje</b>, no contra el actual del chofer: los
/// viajes de un chofer que cambió de transportista siguen apareciendo bajo el de entonces (SC-010).
/// </param>
public record FiltrosDeViajes(
    int? ClienteId = null,
    int? TransportistaId = null,
    EstadoViaje? Estado = null,
    DateOnly? Desde = null,
    DateOnly? Hasta = null,
    string? Busqueda = null,
    int Pagina = 1);

/// <summary>
/// El instante y el día contra los que se derivan las dos señales del listado.
///
/// Llegan por parámetro y no se leen del reloj dentro de la consulta, que es lo que permite fijarlos
/// en un test en vez de esperar cinco días (FR-016, FR-039).
/// </summary>
/// <param name="Hoy">Día en curso en Argentina. Un viaje con fecha anterior es carga retroactiva.</param>
/// <param name="Ahora">Instante UTC del servidor, del que sale el umbral de demora.</param>
public record MomentoDeLectura(DateOnly Hoy, DateTime Ahora)
{
    /// <summary>
    /// Instante a partir del cual un viaje puesto <c>en curso</c> está demorado: <c>ahora − 5 días</c>.
    ///
    /// La consulta compara contra esto en lugar de restar dentro del <c>SELECT</c>, porque así la
    /// condición es una <b>comparación simple</b> que EF Core traduce a SQL directo. Es exactamente
    /// equivalente a <see cref="Viaje.EstaDemorado"/>, que es la escritura en C# de la misma regla:
    /// <c>ahora − desde &gt; 5 días</c> ⟺ <c>desde &lt; ahora − 5 días</c>. Un test compara las dos
    /// sobre el mismo dato, como pide la convención [003].
    /// </summary>
    public DateTime LimiteDeDemora => Ahora.AddDays(-Viaje.DiasParaDemora);

    /// <summary>El momento del servidor, leído del <c>TimeProvider</c> registrado (research §7).</summary>
    public static MomentoDeLectura Desde(TimeProvider reloj)
    {
        var ahora = reloj.GetUtcNow();

        return new MomentoDeLectura(Domain.Choferes.FechaHoyArgentina.Desde(ahora), ahora.UtcDateTime);
    }
}

public interface IRepositorioViajes
{
    Task AgregarAsync(Viaje viaje, CancellationToken cancelacion = default);

    /// <summary>Seguido por el contexto, con sus relaciones cargadas: lo devuelto se modifica.</summary>
    Task<Viaje?> ObtenerParaModificarAsync(int id, CancellationToken cancelacion = default);

    /// <summary>
    /// El viaje con todo lo que la ficha necesita: cliente, chofer con su persona, vehículo,
    /// transportista e historial con el usuario de cada línea (FR-045).
    /// </summary>
    Task<Viaje?> ObtenerFichaAsync(int id, CancellationToken cancelacion = default);

    Task<PaginaDe<ViajeListado>> ConsultarAsync(
        FiltrosDeViajes filtros,
        MomentoDeLectura momento,
        CancellationToken cancelacion = default);

    /// <summary>
    /// El viaje que ya usa ese número de remito entre los <b>no anulados</b>, o <c>null</c> si está
    /// libre. Es lo que permite que el rechazo nombre el viaje que lo tiene (FR-014).
    /// </summary>
    Task<Viaje?> ObtenerPorRemitoAsync(
        string numeroRemito,
        int? idAExcluir = null,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Los choferes que alimentan el desplegable de asignación: los <b>activos</b>, con su persona y
    /// toda su documentación (FR-021).
    ///
    /// La habilitación por documentación <b>no</b> filtra esta lista: se resuelve al asignar, contra
    /// la fecha del viaje. Los documentos vienen igual porque de ellos sale la observación que la
    /// pantalla muestra al lado del nombre, evaluada contra esa misma fecha.
    ///
    /// No pagina: es un desplegable sobre un padrón de decenas de filas.
    /// </summary>
    Task<IReadOnlyList<Domain.Choferes.Chofer>> ConsultarChoferesAsignablesAsync(
        CancellationToken cancelacion = default);

    /// <summary>
    /// Los vehículos que alimentan el desplegable: activos y con <b>estado operativo guardado</b>
    /// <c>disponible</c> —no el derivado contra el día en curso: eso rompería la carga retroactiva,
    /// porque una unidad hoy inhabilitada pudo estar en regla el día del viaje que se está asentando
    /// (FR-021, SC-014)—, con toda su documentación.
    /// </summary>
    Task<IReadOnlyList<Domain.Flota.Vehiculo>> ConsultarVehiculosAsignablesAsync(
        CancellationToken cancelacion = default);

    /// <summary>
    /// El chofer con su persona y toda su documentación, para poder evaluar la habilitación y
    /// nombrarlo en los mensajes. <c>null</c> si no existe.
    /// </summary>
    Task<Domain.Choferes.Chofer?> ObtenerChoferParaAsignarAsync(
        int id,
        CancellationToken cancelacion = default);

    Task<Domain.Flota.Vehiculo?> ObtenerVehiculoParaAsignarAsync(
        int id,
        CancellationToken cancelacion = default);

    /// <summary>
    /// El viaje <c>en curso</c> que ocupa a esa unidad, o <c>null</c> si está libre (FR-026).
    ///
    /// <paramref name="viajeAExcluir"/> deja afuera al viaje sobre el que se está operando: si ya
    /// está en curso con esa misma unidad, no se ocupa a sí mismo.
    ///
    /// Un viaje <c>pendiente</c> nunca ocupa, cualquiera sea su fecha (FR-027).
    /// </summary>
    Task<Viaje?> ViajeEnCursoDelChoferAsync(
        int choferId,
        int viajeAExcluir,
        CancellationToken cancelacion = default);

    Task<Viaje?> ViajeEnCursoDelVehiculoAsync(
        int vehiculoId,
        int viajeAExcluir,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Cambia el estado del viaje y escribe su línea de historial <b>en la misma transacción</b>
    /// (FR-035).
    ///
    /// Va acá y no repetido en los tres casos de uso del ciclo de vida porque la garantía es una
    /// sola: no puede quedar un viaje con el estado cambiado y sin su línea, ni al revés. Un solo
    /// <c>SaveChanges</c> es lo que lo asegura.
    ///
    /// Puede lanzar <see cref="UnidadOcupadaException"/>: es el índice único filtrado cerrando la
    /// carrera que la consulta previa no alcanza a cubrir (SC-005).
    /// </summary>
    Task RegistrarCambioDeEstadoAsync(
        Viaje viaje,
        EstadoViaje estadoNuevo,
        int usuarioId,
        DateTime ocurridoEn,
        CancellationToken cancelacion = default);

    /// <summary>
    /// Los dos cuadros del período, resueltos con <b>dos agregaciones sobre el mismo predicado</b>
    /// (FR-046, FR-046a, FR-047).
    ///
    /// Que el predicado sea el mismo del listado es lo que hace verdadero a SC-008: la suma de los
    /// importes de las filas del listado filtrado por cliente y rango coincide con el total de ese
    /// cliente, porque las dos consultas excluyen los anulados igual.
    /// </summary>
    Task<TotalesDelPeriodo> ConsultarTotalesAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancelacion = default);

    Task GuardarCambiosAsync(CancellationToken cancelacion = default);
}

/// <summary>
/// La carrera por el remito que la consulta previa no alcanza a cerrar: dos altas simultáneas con el
/// mismo remito pasan las dos la verificación y el índice único filtrado corta la segunda
/// (convención [003], SC-003).
/// </summary>
public class RemitoDuplicadoException(Exception interna)
    : Exception("El número de remito ya está cargado en otro viaje.", interna);

/// <summary>
/// La carrera por la exclusividad de una unidad: dos operadores ponen en curso el mismo chofer —o el
/// mismo vehículo— en el mismo milisegundo. La consulta previa deja pasar a los dos y el índice único
/// filtrado corta al segundo. Es lo que hace que el 0% de SC-005 sea una garantía y no una intención.
/// </summary>
/// <param name="EsDelChofer">
/// <c>true</c> si el índice violado fue el del chofer, <c>false</c> si fue el del vehículo. El
/// rechazo tiene código y mensaje distintos para cada uno (FR-026).
/// </param>
public class UnidadOcupadaException(bool esDelChofer, Exception interna)
    : Exception("La unidad ya está en otro viaje en curso.", interna)
{
    public bool EsDelChofer { get; } = esDelChofer;
}
