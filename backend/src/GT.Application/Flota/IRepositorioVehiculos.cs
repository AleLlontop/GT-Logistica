using GT.Application.Choferes;
using GT.Domain.Flota;

namespace GT.Application.Flota;

public interface IRepositorioVehiculos
{
    Task AgregarAsync(Vehiculo vehiculo, CancellationToken cancelacion = default);

    /// <summary>
    /// El vehículo dueño de esa patente, activo o no, o <c>null</c> si está libre.
    ///
    /// Devuelve la entidad y no un <c>bool</c> porque el alta necesita saber <b>si el dueño está
    /// activo</b>: con dueño activo responde <c>patente_duplicada</c> y con dueño inactivo
    /// <c>patente_de_vehiculo_dado_de_baja</c>, que son dos mensajes distintos (FR-002, FR-008f).
    /// </summary>
    /// <param name="idAExcluir">
    /// Al modificar, el propio registro no cuenta como duplicado: conservar la propia patente tiene
    /// que poder guardarse (FR-002).
    /// </param>
    Task<Vehiculo?> ObtenerPorPatenteAsync(
        string patenteNormalizada,
        int? idAExcluir = null,
        CancellationToken cancelacion = default);

    /// <summary>
    /// La unidad con su tipo, su transportista y toda su documentación —con el tipo de cada
    /// documento—, <b>para leer</b>. Es lo que necesita la ficha (FR-038).
    /// </summary>
    Task<Vehiculo?> ObtenerPorIdConRelacionesAsync(int id, CancellationToken cancelacion = default);

    /// <summary>
    /// La unidad <b>seguida por el contexto</b> para poder modificarla, con su documentación y su
    /// tipo cargados.
    ///
    /// Trae la documentación aunque la baja y la reactivación no la necesiten: la modificación sí, y
    /// para el volumen de este módulo —decenas de unidades, cientos de documentos— un solo camino de
    /// lectura vale más que ahorrar una consulta.
    /// </summary>
    Task<Vehiculo?> ObtenerParaModificarAsync(int id, CancellationToken cancelacion = default);

    /// <summary>
    /// Página del listado con los cuatro filtros aplicados sobre toda la flota antes de paginar
    /// (FR-030, FR-032).
    ///
    /// El estado de la documentación, la elección del documento vigente de cada tipo <b>y el estado
    /// operativo derivado</b> se resuelven en SQL, no en memoria: es la única forma de poder filtrar
    /// por <c>disponible</c> sin recorrer toda la flota (research §4 y §5).
    /// </summary>
    /// <param name="hoy">Día en curso en Argentina, contra el que se calculan los estados (FR-020).</param>
    Task<PaginaDe<VehiculoListado>> ConsultarAsync(
        FiltrosDeFlota filtros,
        DateOnly hoy,
        CancellationToken cancelacion = default);

    Task GuardarCambiosAsync(CancellationToken cancelacion = default);
}

/// <summary>
/// Violación del índice único de la patente detectada al guardar. Existe para no filtrar tipos de EF
/// Core ni de SqlClient hacia la capa de aplicación (convención [003]).
///
/// La consulta previa cierra la ventana normal; este índice cierra la carrera entre dos altas
/// simultáneas de la misma patente, que ninguna consulta previa evita (research §6).
/// </summary>
public class PatenteDuplicadaException(Exception interna)
    : Exception("Esa patente ya está registrada en la flota.", interna);
