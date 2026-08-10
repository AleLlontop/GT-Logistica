using GT.Application.Choferes.Documentacion;

namespace GT.Application.Flota.Documentacion;

/// <summary>
/// Descarga del escaneo de un documento de la flota (FR-038, SC-011).
///
/// Siempre por endpoint autorizado y nunca como archivo estático: una póliza o una cédula verde son
/// datos de la empresa, así que conocer la ruta no puede alcanzar para verlos. Exige el <b>mismo</b>
/// permiso que el resto del módulo, aunque sea una lectura de archivo.
/// </summary>
public class DescargarArchivoDocumentoVehiculo(
    IRepositorioDocumentacionVehiculo repositorio,
    IAlmacenDeArchivos almacen)
{
    public record ArchivoParaDescargar(Stream Contenido, string TipoContenido, string Nombre);

    /// <summary>
    /// El archivo, o <c>null</c> si el documento no existe, no tiene adjunto o el adjunto ya no está
    /// en el volumen. Las tres situaciones se comunican igual: <c>no_encontrado</c>.
    /// </summary>
    public async Task<ArchivoParaDescargar?> EjecutarAsync(
        int documentoId,
        CancellationToken cancelacion = default)
    {
        var documento = await repositorio.ObtenerPorIdAsync(documentoId, cancelacion);

        if (documento?.ArchivoRuta is null)
        {
            return null;
        }

        var contenido = await almacen.AbrirAsync(documento.ArchivoRuta, cancelacion);
        if (contenido is null)
        {
            return null;
        }

        return new ArchivoParaDescargar(
            contenido,
            documento.ArchivoTipoContenido ?? "application/octet-stream",
            documento.ArchivoNombre ?? "documento");
    }
}
