using GT.Domain.Choferes;

using DocumentoDeChofer = GT.Domain.Choferes.Documentacion;

namespace GT.Application.Choferes.Documentacion;

public interface IRepositorioDocumentacion
{
    /// <summary>El documento con su tipo cargado, que hace falta para calcular su estado.</summary>
    Task<DocumentoDeChofer?> ObtenerPorIdAsync(int id, CancellationToken cancelacion = default);

    /// <summary>Todos los documentos del chofer, con su tipo. Para decidir cuál es el vigente.</summary>
    Task<List<DocumentoDeChofer>> ConsultarDelChoferAsync(int choferId, CancellationToken cancelacion = default);

    Task<bool> ExisteChoferAsync(int choferId, CancellationToken cancelacion = default);

    /// <summary>El tipo, sólo si está activo: no se puede cargar un documento de un tipo dado de baja.</summary>
    Task<DocumentacionTipo?> ObtenerTipoActivoAsync(int tipoId, CancellationToken cancelacion = default);

    Task AgregarAsync(DocumentoDeChofer documento, CancellationToken cancelacion = default);

    Task EliminarAsync(DocumentoDeChofer documento, CancellationToken cancelacion = default);

    Task GuardarCambiosAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Los documentos vigentes de cada tipo de los choferes <b>activos</b>, con su tipo, su chofer,
    /// la persona y el transportista cargados. Es la materia prima del panel de vencimientos.
    ///
    /// El filtro por chofer activo y la elección del vigente se resuelven en la base; qué documentos
    /// alertan lo decide después la regla del dominio, que es la misma que usa la ficha (FR-021).
    /// </summary>
    Task<List<DocumentoDeChofer>> ConsultarVigentesDeChoferesActivosAsync(
        CancellationToken cancelacion = default);
}
