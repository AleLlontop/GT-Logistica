using GT.Domain.Choferes;

// El módulo tiene una carpeta `Documentacion/` y el dominio una entidad `Documentacion`. Dentro de
// este espacio de nombres gana la carpeta, así que el tipo se nombra por alias en vez de escribirlo
// entero en cada firma.
using DocumentoDeChofer = GT.Domain.Choferes.Documentacion;

namespace GT.Application.Choferes;

public enum ErrorChofer
{
    Ninguno,
    NoEncontrado,
    DatosInvalidos,
    DniDuplicado,
    CuilDuplicado,
    TransportistaInexistente,
    MenorDeEdad,

    /// <summary>Se pidió reactivar un chofer que ya estaba activo (FR-005b).</summary>
    YaEstaActivo,
}

/// <param name="ReutilizoPersona">
/// <c>true</c> si el alta tomó una persona que ya estaba en el padrón del Módulo 2 en vez de crear
/// una nueva (FR-006). Es lo que decide cuál de los dos textos de confirmación muestra la pantalla
/// (<c>contracts/README.md</c>).
/// </param>
public record ResultadoChofer(
    ErrorChofer Error,
    ChoferDetalle? Chofer = null,
    string? Campo = null,
    bool ReutilizoPersona = false)
{
    public bool Exitoso => Error is ErrorChofer.Ninguno;
}

public record ChoferRequest(
    string? Nombre,
    string? Apellido,
    string? Dni,
    string? Cuil,
    string? FechaNacimiento,
    string? Telefono,
    string? Email,
    int? TransportistaId);

public record TransportistaResumen(int Id, string Nombre);

public record TipoDocumentacionResumen(int Id, string Nombre);

/// <summary>
/// Un documento del chofer tal como lo devuelve el contrato. El <c>estado</c> y
/// <c>diasHastaVencimiento</c> se calculan al leer, nunca se guardan (FR-018, research §2).
/// </summary>
public record DocumentoDto(
    int Id,
    TipoDocumentacionResumen Tipo,
    string Numero,
    string FechaEmision,
    string FechaVencimiento,
    string Estado,
    bool EsVigenteDelTipo,
    int DiasHastaVencimiento,
    bool TieneArchivo,
    string? ArchivoNombre)
{
    /// <param name="esVigenteDelTipo">
    /// Si es el documento que manda para su tipo. Lo decide quien tiene a la vista los demás
    /// documentos del chofer, no el documento solo (FR-020a).
    /// </param>
    public static DocumentoDto Desde(DocumentoDeChofer documento, bool esVigenteDelTipo, DateOnly hoy)
    {
        var tipo = documento.Tipo
            ?? throw new InvalidOperationException(
                $"El documento {documento.Id} llegó sin su tipo cargado, y sin los días de aviso no " +
                "se puede calcular su estado.");

        return new DocumentoDto(
            documento.Id,
            new TipoDocumentacionResumen(tipo.Id, tipo.Nombre),
            documento.Numero,
            documento.FechaEmision.ToString("yyyy-MM-dd"),
            documento.FechaVencimiento.ToString("yyyy-MM-dd"),
            NombresDeEstado.DelDocumento(
                CalculadorEstadoDocumento.Calcular(
                    documento.FechaVencimiento,
                    tipo.DiasAvisoVencimiento,
                    hoy)),
            esVigenteDelTipo,
            CalculadorEstadoDocumento.DiasHastaVencimiento(documento.FechaVencimiento, hoy),
            documento.TieneArchivo,
            documento.ArchivoNombre);
    }
}

public record ChoferDetalle(
    int Id,
    string Apellido,
    string Nombre,
    string Dni,
    string Cuil,
    string FechaNacimiento,
    string Telefono,
    string Email,
    TransportistaResumen Transportista,
    bool Activo,
    string EstadoDocumentacion,
    int PersonaId,
    IReadOnlyList<DocumentoDto> Documentos)
{
    /// <summary>
    /// Sólo tiene sentido en la respuesta del alta: <c>true</c> cuando el DNI ya estaba en el padrón
    /// y se reutilizó esa persona en vez de crear una nueva (FR-006).
    ///
    /// Es el único dato que le falta a la pantalla para elegir entre los dos textos de confirmación
    /// que fija <c>contracts/README.md</c> —"se registró correctamente" y "se registró correctamente,
    /// reutilizando la persona que ya estaba en el padrón"—. En la ficha siempre viene en
    /// <c>false</c> y no se muestra.
    /// </summary>
    public bool ReutilizoPersona { get; init; }

    public static ChoferDetalle Desde(Chofer chofer, DateOnly hoy)
    {
        var persona = chofer.Persona
            ?? throw new InvalidOperationException(
                $"El chofer {chofer.Id} llegó sin su persona cargada; sin ella no hay datos que devolver.");

        var transportista = chofer.Transportista
            ?? throw new InvalidOperationException(
                $"El chofer {chofer.Id} llegó sin su transportista cargado.");

        var estado = CalculadorEstadoChofer.Calcular(chofer.Documentacion, hoy);

        var vigentes = CalculadorEstadoChofer
            .VigentesDeCadaTipo(chofer.Documentacion)
            .Select(documento => documento.Id)
            .ToHashSet();

        // Agrupados por tipo y, dentro de cada tipo, por vencimiento descendente: el vigente
        // primero y sus renovaciones anteriores debajo (contracts/choferes-api.yaml).
        var documentos = chofer.Documentacion
            .OrderBy(documento => documento.Tipo?.Nombre)
            .ThenByDescending(documento => documento.FechaVencimiento)
            .ThenByDescending(documento => documento.Id)
            .Select(documento => DocumentoDto.Desde(documento, vigentes.Contains(documento.Id), hoy))
            .ToList();

        return new ChoferDetalle(
            chofer.Id,
            persona.Apellido,
            persona.Nombre,
            persona.Dni,
            chofer.Cuil,
            persona.FechaNacimiento.ToString("yyyy-MM-dd"),
            persona.Telefono,
            persona.Email,
            new TransportistaResumen(transportista.Id, transportista.Nombre),
            chofer.Activo,
            NombresDeEstado.DelChofer(estado),
            chofer.PersonaId,
            documentos);
    }

}

/// <summary>
/// Los enums del dominio viajan en el JSON con la misma grafía que fija el contrato
/// (<c>enRegla</c>, <c>proximaAvencer</c>, …), que es camelCase y no PascalCase.
/// </summary>
public static class NombresDeEstado
{
    public static string DelChofer(EstadoDocumentacionChofer estado) => EnCamelCase(estado.ToString());

    public static string DelDocumento(DocumentacionEstado estado) => EnCamelCase(estado.ToString());

    /// <summary>Ámbito de un tipo de documentación: <c>chofer</c> o <c>vehiculo</c> (Módulo 4, FR-017).</summary>
    public static string DelAmbito(DocumentacionAmbito ambito) => EnCamelCase(ambito.ToString());

    private static string EnCamelCase(string nombre) =>
        char.ToLowerInvariant(nombre[0]) + nombre[1..];
}

public static class ValidadorChofer
{
    public static string? PrimerCampoInvalido(ChoferRequest peticion)
    {
        if (string.IsNullOrWhiteSpace(peticion.Nombre)) return "nombre";
        if (string.IsNullOrWhiteSpace(peticion.Apellido)) return "apellido";
        if (string.IsNullOrWhiteSpace(peticion.Dni)) return "dni";
        if (string.IsNullOrWhiteSpace(peticion.Cuil)) return "cuil";
        if (!DateOnly.TryParse(peticion.FechaNacimiento, out _)) return "fechaNacimiento";
        if (string.IsNullOrWhiteSpace(peticion.Telefono)) return "telefono";
        if (string.IsNullOrWhiteSpace(peticion.Email) || !peticion.Email.Contains('@')) return "email";
        if (peticion.TransportistaId is null or <= 0) return "transportistaId";

        return null;
    }
}
