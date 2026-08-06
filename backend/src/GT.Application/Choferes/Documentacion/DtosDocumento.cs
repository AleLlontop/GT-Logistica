namespace GT.Application.Choferes.Documentacion;

public enum ErrorDocumento
{
    Ninguno,
    NoEncontrado,
    ChoferNoEncontrado,
    DatosInvalidos,
    TipoInexistente,
    VencimientoAnteriorAEmision,
    ArchivoNoAdmitido,
    ArchivoNoGuardado,
}

public record ResultadoDocumento(
    ErrorDocumento Error,
    DocumentoDto? Documento = null,
    string? Campo = null)
{
    public bool Exitoso => Error is ErrorDocumento.Ninguno;
}

/// <summary>
/// Lo que llega del formulario. <b>No hay campo de estado</b>, a propósito: lo calcula el sistema y
/// no se recibe por ninguna vía (FR-018, SC-004).
/// </summary>
public record DocumentoRequest(
    int? DocumentacionTipoId,
    string? Numero,
    string? FechaEmision,
    string? FechaVencimiento);

/// <summary>
/// El archivo cargado, ya desprendido de <c>IFormFile</c> para que la capa de aplicación no dependa
/// de ASP.NET Core. <c>null</c> significa que no vino ninguno, que es válido (FR-015).
/// </summary>
public record ArchivoCargado(string Nombre, long TamanioEnBytes, Func<Stream> Abrir);

public static class ValidadorDocumento
{
    public static string? PrimerCampoInvalido(DocumentoRequest peticion)
    {
        if (peticion.DocumentacionTipoId is null or <= 0) return "documentacionTipoId";
        if (string.IsNullOrWhiteSpace(peticion.Numero)) return "numero";
        if (peticion.Numero.Trim().Length > 50) return "numero";
        if (!DateOnly.TryParse(peticion.FechaEmision, out _)) return "fechaEmision";
        if (!DateOnly.TryParse(peticion.FechaVencimiento, out _)) return "fechaVencimiento";

        return null;
    }
}
