using GT.Domain.Choferes;

namespace GT.Application.Choferes.Documentacion;

public interface IRepositorioTiposDocumentacion
{
    /// <summary>
    /// El catálogo, con cuántos documentos usa cada tipo. Puede venir vacío: arranca así y no se
    /// precarga por migración.
    /// </summary>
    Task<List<TipoConDocumentos>> ConsultarAsync(bool soloActivos, CancellationToken cancelacion);

    Task<TipoConDocumentos?> ObtenerConDocumentosAsync(int id, CancellationToken cancelacion);

    Task<DocumentacionTipo?> ObtenerPorIdAsync(int id, CancellationToken cancelacion);

    Task<bool> ExisteNombreAsync(string nombre, int? idAExcluir, CancellationToken cancelacion);

    Task<int> ContarDocumentosAsync(int tipoId, CancellationToken cancelacion);

    Task AgregarAsync(DocumentacionTipo tipo, CancellationToken cancelacion);

    Task GuardarCambiosAsync(CancellationToken cancelacion);
}

/// <summary>
/// Violación del índice único del nombre detectada al guardar. Cierra la carrera entre dos altas
/// simultáneas, que ninguna consulta previa puede evitar.
/// </summary>
public class NombreDeTipoDuplicadoException(Exception interna)
    : Exception("Ya existe un tipo de documentación con ese nombre.", interna);
