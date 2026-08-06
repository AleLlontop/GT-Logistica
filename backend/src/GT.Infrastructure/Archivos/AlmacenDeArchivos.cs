using GT.Application.Choferes.Documentacion;
using Microsoft.Extensions.Logging;

namespace GT.Infrastructure.Archivos;

/// <summary>
/// Almacén de escaneos sobre un volumen del compose, bajo la ruta de <c>GT_ARCHIVOS_RUTA</c>
/// (research §3).
///
/// Tres cosas que definen esta clase:
/// <list type="bullet">
///   <item><b>El nombre en disco lo genera el sistema.</b> Nunca se usa el nombre cargado por el
///   usuario: puede contener <c>../</c> y escaparse del directorio, o repetirse y pisar otro
///   documento. El nombre original queda en la fila, sólo para mostrarlo y para la descarga.</item>
///   <item><b>Los archivos se reparten en subcarpetas por año y mes.</b> Un único directorio con
///   miles de entradas se vuelve incómodo de mirar y de respaldar.</item>
///   <item><b>Toda ruta que llega desde afuera se verifica contra la raíz</b> antes de tocar el
///   disco. Es defensa en profundidad: las rutas las genera esta misma clase, pero viajan por una
///   columna de la base y no conviene confiar en que sigan siendo las que se escribieron.</item>
/// </list>
/// </summary>
public class AlmacenDeArchivos : IAlmacenDeArchivos
{
    private readonly string _raiz;
    private readonly ILogger<AlmacenDeArchivos> _log;

    public AlmacenDeArchivos(string raiz, ILogger<AlmacenDeArchivos> log)
    {
        _raiz = Path.GetFullPath(raiz);
        _log = log;
    }

    public async Task<string> GuardarAsync(Stream contenido, CancellationToken cancelacion = default)
    {
        var hoy = DateTime.UtcNow;
        var subcarpeta = Path.Combine(hoy.Year.ToString("D4"), hoy.Month.ToString("D2"));
        var rutaRelativa = Path.Combine(subcarpeta, $"{Guid.NewGuid():N}.bin");
        var rutaAbsoluta = ResolverDentroDeLaRaiz(rutaRelativa);

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(rutaAbsoluta)!);

            await using var destino = new FileStream(
                rutaAbsoluta,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None);

            await contenido.CopyToAsync(destino, cancelacion);
        }
        catch (Exception excepcion)
        {
            _log.LogError(excepcion, "No se pudo escribir el adjunto en {Ruta}.", rutaAbsoluta);

            // Si quedó a medias, que no quede a medias.
            await BorrarAsync(rutaRelativa, CancellationToken.None);

            throw new ArchivoNoGuardadoException("No se pudo escribir el archivo adjunto.", excepcion);
        }

        // Se guarda con barras hacia adelante para que la columna no dependa del sistema operativo.
        return rutaRelativa.Replace(Path.DirectorySeparatorChar, '/');
    }

    public Task<Stream?> AbrirAsync(string rutaRelativa, CancellationToken cancelacion = default)
    {
        var rutaAbsoluta = ResolverDentroDeLaRaiz(rutaRelativa);

        if (!File.Exists(rutaAbsoluta))
        {
            return Task.FromResult<Stream?>(null);
        }

        Stream contenido = new FileStream(rutaAbsoluta, FileMode.Open, FileAccess.Read, FileShare.Read);
        return Task.FromResult<Stream?>(contenido);
    }

    public Task BorrarAsync(string rutaRelativa, CancellationToken cancelacion = default)
    {
        try
        {
            var rutaAbsoluta = ResolverDentroDeLaRaiz(rutaRelativa);

            if (File.Exists(rutaAbsoluta))
            {
                File.Delete(rutaAbsoluta);
            }
        }
        catch (Exception excepcion)
        {
            // Un adjunto que no se pudo borrar deja basura en el volumen, y eso es todo: no puede
            // hacer fallar la operación que ya se confirmó en la base (research §10).
            _log.LogWarning(excepcion, "No se pudo borrar el adjunto {Ruta}.", rutaRelativa);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Convierte una ruta relativa en absoluta y verifica que no se haya escapado de la raíz. Una
    /// ruta con <c>../</c> o absoluta se rechaza en vez de tocar un archivo de otro lado.
    /// </summary>
    private string ResolverDentroDeLaRaiz(string rutaRelativa)
    {
        if (string.IsNullOrWhiteSpace(rutaRelativa) || Path.IsPathRooted(rutaRelativa))
        {
            throw new ArgumentException(
                "La ruta del adjunto tiene que ser relativa al volumen.",
                nameof(rutaRelativa));
        }

        var candidata = Path.GetFullPath(Path.Combine(_raiz, rutaRelativa));

        if (!candidata.StartsWith(_raiz + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "La ruta del adjunto queda fuera del volumen de archivos.",
                nameof(rutaRelativa));
        }

        return candidata;
    }
}
