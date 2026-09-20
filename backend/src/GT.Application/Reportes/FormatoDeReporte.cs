namespace GT.Application.Reportes;

/// <summary>Los dos formatos que fija FR-002. Dos valores, y no hay un tercero.</summary>
public enum FormatoDeReporte
{
    Pdf,
    Excel,
}

/// <summary>
/// Los <b>tres</b> derivados del formato —tipo de contenido, extensión y disposición—, todos en un
/// solo lugar (data-model §5).
///
/// Que los tres salgan del mismo enum es lo que impide que el backend sirva un <c>.xlsx</c> diciendo
/// que es un PDF.
/// </summary>
public static class FormatosDeReporte
{
    public const string TipoDeContenidoPdf = "application/pdf";

    public const string TipoDeContenidoExcel =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>
    /// Lee <c>?formato=pdf|excel</c>. <c>null</c> cuando falta o no es ninguno de los dos, y quien
    /// llama lo rechaza con <c>400 formato_invalido</c> (contracts §Cuerpos de error).
    ///
    /// **Estricto**, a diferencia del filtro de estado del listado de viajes: ahí filtrar de más no es
    /// un error; acá el formato decide qué archivo se arma y no tiene valor por defecto.
    /// </summary>
    public static FormatoDeReporte? Leer(string? valor) => valor switch
    {
        "pdf" => FormatoDeReporte.Pdf,
        "excel" => FormatoDeReporte.Excel,
        _ => null,
    };

    public static string TipoDeContenido(FormatoDeReporte formato) => formato switch
    {
        FormatoDeReporte.Pdf => TipoDeContenidoPdf,
        FormatoDeReporte.Excel => TipoDeContenidoExcel,
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, null),
    };

    public static string Extension(FormatoDeReporte formato) => formato switch
    {
        FormatoDeReporte.Pdf => ".pdf",
        FormatoDeReporte.Excel => ".xlsx",
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, null),
    };

    /// <summary>
    /// <c>inline</c> para el PDF y <c>attachment</c> para el Excel.
    ///
    /// Quién decide cómo se sirve el contenido es el backend y no el enlace (convención [003]): quien
    /// abre un PDF lo quiere ver, y una planilla se baja para trabajarla.
    /// </summary>
    public static string Disposicion(FormatoDeReporte formato) => formato switch
    {
        FormatoDeReporte.Pdf => "inline",
        FormatoDeReporte.Excel => "attachment",
        _ => throw new ArgumentOutOfRangeException(nameof(formato), formato, null),
    };
}
