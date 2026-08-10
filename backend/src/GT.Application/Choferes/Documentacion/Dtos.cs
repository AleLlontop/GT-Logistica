using GT.Domain.Choferes;

namespace GT.Application.Choferes.Documentacion;

public enum ErrorTipoDocumentacion
{
    Ninguno,
    NoEncontrado,
    DatosInvalidos,
    NombreDuplicado,
    ConDocumentos,

    /// <summary>
    /// Se quiso cambiar el ámbito de un tipo que ya tiene documentos (Módulo 4, FR-017d). Si se
    /// permitiera, esos documentos quedarían colgando de un tipo que su propio módulo ya no ofrece y
    /// su formulario de corrección no podría volver a elegirlo.
    /// </summary>
    AmbitoNoModificable,
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

/// <param name="Ambito">
/// <c>chofer</c> o <c>vehiculo</c>, en camelCase como el resto de los enums del contrato.
/// <b>Obligatorio</b> desde el Módulo 4 (FR-017).
/// </param>
public record TipoDocumentacionRequest(string? Nombre, int? DiasAvisoVencimiento, string? Ambito);

/// <summary>Un tipo con la cantidad de documentos que lo usan, que es lo que impide su baja.</summary>
public record TipoConDocumentos(DocumentacionTipo Tipo, int DocumentosAsociados);

/// <param name="DocumentosAsociados">
/// Desde el Módulo 4 suma <b>las dos</b> tablas —documentos de choferes y de vehículos— (FR-017b). Es
/// lo que impide la baja y el cambio de ámbito. Cambia hacia el lado seguro: bloquea más bajas, nunca
/// menos.
/// </param>
public record TipoDocumentacionDto(
    int Id,
    string Nombre,
    int DiasAvisoVencimiento,
    string Ambito,
    bool Activo,
    int DocumentosAsociados)
{
    public static TipoDocumentacionDto Desde(DocumentacionTipo tipo, int documentosAsociados = 0) =>
        new(
            tipo.Id,
            tipo.Nombre,
            tipo.DiasAvisoVencimiento,
            NombresDeEstado.DelAmbito(tipo.Ambito),
            tipo.Activo,
            documentosAsociados);

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

        // Obligatorio desde el Módulo 4: sin ámbito no se sabe en qué módulo se ofrece (FR-017).
        if (LeerAmbito(peticion.Ambito) is null) return "ambito";

        return null;
    }

    /// <summary>
    /// El ámbito llega en camelCase, como el resto de los enums del contrato. Un valor desconocido
    /// devuelve <c>null</c> y el campo se marca como inválido, en vez de caer en un valor por defecto
    /// que nadie eligió.
    /// </summary>
    public static DocumentacionAmbito? LeerAmbito(string? valor) => valor switch
    {
        "chofer" => DocumentacionAmbito.Chofer,
        "vehiculo" => DocumentacionAmbito.Vehiculo,
        _ => null,
    };
}
