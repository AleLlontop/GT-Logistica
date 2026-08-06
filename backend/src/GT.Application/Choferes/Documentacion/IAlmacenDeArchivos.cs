namespace GT.Application.Choferes.Documentacion;

/// <summary>
/// Guarda, recupera y borra los escaneos de la documentación.
///
/// Vive acá como interfaz para que los casos de uso no dependan del sistema de archivos, y para
/// poder sustituirlo por uno que falla y verificar la atomicidad de FR-015e sin romper nada real.
/// </summary>
public interface IAlmacenDeArchivos
{
    /// <summary>
    /// Escribe el archivo y devuelve su ruta relativa dentro del volumen. El nombre en disco lo
    /// genera el almacén: nunca se usa el que cargó el usuario, que puede traer <c>../</c> o
    /// repetirse y pisar otro documento (research §3).
    /// </summary>
    Task<string> GuardarAsync(Stream contenido, CancellationToken cancelacion = default);

    /// <summary>El contenido, o <c>null</c> si la ruta ya no existe en el volumen.</summary>
    Task<Stream?> AbrirAsync(string rutaRelativa, CancellationToken cancelacion = default);

    /// <summary>
    /// Borra el archivo. No falla si ya no está: el objetivo es que deje de existir, y que otro se
    /// haya adelantado no es un error.
    /// </summary>
    Task BorrarAsync(string rutaRelativa, CancellationToken cancelacion = default);
}

/// <summary>
/// El archivo era válido pero no se pudo guardar. Se traduce a <c>archivo_no_guardado</c>: la
/// operación queda sin efecto y la pantalla conserva lo tipeado para reintentar (FR-015e).
/// </summary>
public class ArchivoNoGuardadoException(string mensaje, Exception? interna = null)
    : Exception(mensaje, interna);

/// <param name="TipoContenido">
/// El tipo <b>deducido de la firma</b> del archivo, que es el que se guarda en la fila. Nunca el que
/// declaró el navegador: eso lo controla quien sube (FR-015a, research §3).
/// </param>
public record ValidacionDeArchivo(bool EsValido, string? TipoContenido);

/// <summary>
/// Decide si un archivo cargado se admite. Vive acá como interfaz porque el reconocimiento de firmas
/// es infraestructura, y los casos de uso sólo necesitan la respuesta.
/// </summary>
public interface IValidadorDeArchivo
{
    Task<ValidacionDeArchivo> ValidarAsync(
        Stream contenido,
        long tamanioEnBytes,
        CancellationToken cancelacion = default);
}
