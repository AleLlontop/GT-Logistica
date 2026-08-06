using GT.Domain.Choferes;

namespace GT.Application.Choferes.Documentacion;

public enum ErrorTipoDocumentacion
{
    Ninguno,
    NoEncontrado,
    DatosInvalidos,
    NombreDuplicado,
    ConDocumentos,
}

public record ResultadoTipoDocumentacion(
    ErrorTipoDocumentacion Error,
    TipoDocumentacionDto? Tipo = null,
    string? Campo = null)
{
    public bool Exitoso => Error is ErrorTipoDocumentacion.Ninguno;

    /// <summary>Cuántos documentos usan el tipo, para poder decirlo en el mensaje (FR-014).</summary>
    public int? CantidadDocumentos { get; init; }
}

public record TipoDocumentacionRequest(string? Nombre, int? DiasAvisoVencimiento);

/// <summary>Un tipo con la cantidad de documentos que lo usan, que es lo que impide su baja.</summary>
public record TipoConDocumentos(DocumentacionTipo Tipo, int DocumentosAsociados);

public record TipoDocumentacionDto(
    int Id,
    string Nombre,
    int DiasAvisoVencimiento,
    bool Activo,
    int DocumentosAsociados)
{
    public static TipoDocumentacionDto Desde(DocumentacionTipo tipo, int documentosAsociados = 0) =>
        new(tipo.Id, tipo.Nombre, tipo.DiasAvisoVencimiento, tipo.Activo, documentosAsociados);

    public static TipoDocumentacionDto Desde(TipoConDocumentos fila) =>
        Desde(fila.Tipo, fila.DocumentosAsociados);
}

public static class ValidadorTipoDocumentacion
{
    public static string? PrimerCampoInvalido(TipoDocumentacionRequest peticion)
    {
        if (string.IsNullOrWhiteSpace(peticion.Nombre)) return "nombre";
        if (peticion.Nombre.Trim().Length > 100) return "nombre";

        // Cero es válido y significa "sin período de aviso intermedio" (FR-013, caso límite).
        if (peticion.DiasAvisoVencimiento is null or < 0) return "diasAvisoVencimiento";

        return null;
    }
}
