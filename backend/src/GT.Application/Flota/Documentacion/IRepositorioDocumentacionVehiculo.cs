using GT.Domain.Choferes;
using GT.Domain.Flota;

namespace GT.Application.Flota.Documentacion;

public interface IRepositorioDocumentacionVehiculo
{
    /// <summary>El documento con su tipo cargado, que hace falta para calcular su estado.</summary>
    Task<DocumentacionVehiculo?> ObtenerPorIdAsync(int id, CancellationToken cancelacion = default);

    /// <summary>Todos los documentos del vehículo, con su tipo. Para decidir cuál es el vigente.</summary>
    Task<List<DocumentacionVehiculo>> ConsultarDelVehiculoAsync(
        int vehiculoId,
        CancellationToken cancelacion = default);

    Task<bool> ExisteVehiculoAsync(int vehiculoId, CancellationToken cancelacion = default);

    /// <summary>
    /// El tipo, sólo si está <b>activo y es de ámbito vehículo</b> (FR-017a). Devolver <c>null</c>
    /// para un tipo de chofer es lo que hace que mandar su identificador a mano se rechace igual que
    /// si no existiera (US3 esc. 12).
    /// </summary>
    Task<DocumentacionTipo?> ObtenerTipoActivoDeVehiculoAsync(
        int tipoId,
        CancellationToken cancelacion = default);

    Task AgregarAsync(DocumentacionVehiculo documento, CancellationToken cancelacion = default);

    /// <summary>Borrado físico: el documento no lleva baja lógica (FR-027, FR-028).</summary>
    Task EliminarAsync(DocumentacionVehiculo documento, CancellationToken cancelacion = default);

    Task GuardarCambiosAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Los documentos vigentes de cada tipo de los vehículos <b>activos</b>, con su tipo, su vehículo
    /// y el transportista cargados. Es la materia prima del panel de vencimientos.
    ///
    /// El filtro por vehículo activo y la elección del vigente se resuelven en la base; qué
    /// documentos alertan lo decide después la regla del dominio, que es la misma que usa la ficha
    /// (FR-035).
    /// </summary>
    Task<List<DocumentacionVehiculo>> ConsultarVigentesDeVehiculosActivosAsync(
        CancellationToken cancelacion = default);
}
