using GT.Application.Choferes.Documentacion;

namespace GT.Infrastructure.Archivos;

/// <summary>Lo que se puede deducir de un archivo cargado: si sirve y qué es realmente.</summary>
public record ResultadoValidacionArchivo(bool EsValido, string? TipoContenido)
{
    public static readonly ResultadoValidacionArchivo NoAdmitido = new(false, null);

    public static ResultadoValidacionArchivo Admitido(string tipoContenido) => new(true, tipoContenido);
}

/// <summary>
/// Restricciones del archivo adjunto (FR-015a): PDF, JPG o PNG, hasta 10 MB.
///
/// <b>El tipo se determina por la firma del archivo, no por su extensión ni por el
/// <c>Content-Type</c> que declara el navegador</b>: las dos cosas las controla quien sube, y
/// renombrar un ejecutable a <c>.pdf</c> no tiene que alcanzar para que entre (research §3).
///
/// El tipo que se guarda en la fila es el deducido acá, no el declarado, así que la descarga
/// tampoco puede terminar devolviendo algo que diga ser lo que no es.
/// </summary>
public static class ValidadorArchivo
{
    public const long TamanioMaximoEnBytes = 10 * 1024 * 1024;

    public const string TipoPdf = "application/pdf";
    public const string TipoJpeg = "image/jpeg";
    public const string TipoPng = "image/png";

    /// <summary>Lo que la pantalla informa antes de que alguien elija un archivo.</summary>
    public const string Descripcion = "PDF, JPG o PNG, hasta 10 MB";

    private static readonly byte[] FirmaPdf = "%PDF"u8.ToArray();
    private static readonly byte[] FirmaJpeg = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] FirmaPng = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>La firma más larga que hay que leer para poder decidir.</summary>
    public const int BytesNecesarios = 8;

    public static ResultadoValidacionArchivo Validar(ReadOnlySpan<byte> comienzo, long tamanioEnBytes)
    {
        if (tamanioEnBytes <= 0 || tamanioEnBytes > TamanioMaximoEnBytes)
        {
            return ResultadoValidacionArchivo.NoAdmitido;
        }

        if (comienzo.StartsWith(FirmaPdf)) return ResultadoValidacionArchivo.Admitido(TipoPdf);
        if (comienzo.StartsWith(FirmaJpeg)) return ResultadoValidacionArchivo.Admitido(TipoJpeg);
        if (comienzo.StartsWith(FirmaPng)) return ResultadoValidacionArchivo.Admitido(TipoPng);

        return ResultadoValidacionArchivo.NoAdmitido;
    }

    /// <summary>
    /// Lee del flujo lo justo para reconocer la firma y lo deja donde estaba, para que quien guarde
    /// después escriba el archivo completo.
    /// </summary>
    public static async Task<ResultadoValidacionArchivo> ValidarAsync(
        Stream contenido,
        long tamanioEnBytes,
        CancellationToken cancelacion = default)
    {
        if (tamanioEnBytes <= 0 || tamanioEnBytes > TamanioMaximoEnBytes)
        {
            return ResultadoValidacionArchivo.NoAdmitido;
        }

        var comienzo = new byte[BytesNecesarios];
        var leidos = await contenido.ReadAtLeastAsync(
            comienzo,
            BytesNecesarios,
            throwOnEndOfStream: false,
            cancelacion);

        if (contenido.CanSeek)
        {
            contenido.Seek(0, SeekOrigin.Begin);
        }

        return Validar(comienzo.AsSpan(0, leidos), tamanioEnBytes);
    }
}

/// <summary>
/// Adaptador del validador estático a la interfaz que consumen los casos de uso. Existe sólo para
/// que la capa de aplicación no tenga que conocer las firmas de archivo.
/// </summary>
public class ValidadorDeArchivoPorFirma : IValidadorDeArchivo
{
    public async Task<ValidacionDeArchivo> ValidarAsync(
        Stream contenido,
        long tamanioEnBytes,
        CancellationToken cancelacion = default)
    {
        var resultado = await ValidadorArchivo.ValidarAsync(contenido, tamanioEnBytes, cancelacion);

        return new ValidacionDeArchivo(resultado.EsValido, resultado.TipoContenido);
    }
}
