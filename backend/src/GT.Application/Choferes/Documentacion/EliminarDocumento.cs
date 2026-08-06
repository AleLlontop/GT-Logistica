namespace GT.Application.Choferes.Documentacion;

/// <summary>
/// Eliminación definitiva de un documento (FR-015c, FR-015d).
///
/// Es la única entidad del módulo que se borra de verdad, y va a propósito contra la convención de
/// baja lógica del resto: un documento cargado por error no es un hecho histórico que convenga
/// conservar, y encima puede tapar el estado real del chofer, porque el vigente de cada tipo es el
/// de vencimiento más lejano (research §10).
///
/// Primero la fila y después el archivo, nunca al revés: si el proceso se cayera en el medio, sobra
/// un archivo que nadie referencia en vez de faltar el archivo de una fila que dice tenerlo.
///
/// Si el eliminado era el vigente de su tipo, el anterior vuelve a mandar y el estado del chofer
/// cambia solo, sin actualizar ninguna fila (FR-020a).
/// </summary>
public class EliminarDocumento(IRepositorioDocumentacion repositorio, IAlmacenDeArchivos almacen)
{
    public async Task<ResultadoDocumento> EjecutarAsync(int id, CancellationToken cancelacion = default)
    {
        var documento = await repositorio.ObtenerPorIdAsync(id, cancelacion);
        if (documento is null)
        {
            return new ResultadoDocumento(ErrorDocumento.NoEncontrado);
        }

        var ruta = documento.ArchivoRuta;

        await repositorio.EliminarAsync(documento, cancelacion);
        await repositorio.GuardarCambiosAsync(cancelacion);

        if (ruta is not null)
        {
            await almacen.BorrarAsync(ruta, CancellationToken.None);
        }

        return new ResultadoDocumento(ErrorDocumento.Ninguno);
    }
}

/// <summary>
/// Descarga del escaneo (FR-024).
///
/// Siempre por endpoint autorizado y nunca como archivo estático: un psicofísico o una licencia son
/// datos personales sensibles, así que conocer la ruta no puede alcanzar para verlos (research §3).
/// </summary>
public class DescargarArchivoDocumento(
    IRepositorioDocumentacion repositorio,
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
