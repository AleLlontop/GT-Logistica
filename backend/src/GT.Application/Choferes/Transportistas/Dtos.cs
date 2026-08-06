using GT.Domain.Choferes;

namespace GT.Application.Choferes.Transportistas;

public enum ErrorTransportista
{
    Ninguno,
    NoEncontrado,
    DatosInvalidos,
    CuitDuplicado,
    ConChoferes,
}

public record ResultadoTransportista(ErrorTransportista Error, TransportistaDto? Transportista, string? Campo = null)
{
    public bool Exitoso => Error is ErrorTransportista.Ninguno;
    public int? CantidadChoferes { get; init; }
}

public record TransportistaRequest(
    string? Nombre,
    string? Cuit,
    string? Tipo,
    string? Telefono,
    string? Email);

/// <summary>
/// Un transportista con la cantidad de choferes activos que dependen de él, que es lo que impide su
/// baja (FR-010) y la columna que el listado muestra. Se resuelve en la consulta, no trayendo los
/// choferes a memoria.
/// </summary>
public record TransportistaConChoferesActivos(Transportista Transportista, int ChoferesActivos);

public record TransportistaDto(
    int Id,
    string Nombre,
    string Cuit,
    string Tipo,
    string Telefono,
    string Email,
    bool Activo,
    int ChoferesActivos)
{
    public static TransportistaDto Desde(Transportista transportista, int choferesActivos = 0) => new(
        transportista.Id,
        transportista.Nombre,
        transportista.Cuit,
        transportista.Tipo.ToString().ToLowerInvariant(),
        transportista.Telefono,
        transportista.Email,
        transportista.Activo,
        choferesActivos);

    public static TransportistaDto Desde(TransportistaConChoferesActivos fila) =>
        Desde(fila.Transportista, fila.ChoferesActivos);
}

public static class ValidadorTransportista
{
    public static string? PrimerCampoInvalido(TransportistaRequest peticion)
    {
        if (string.IsNullOrWhiteSpace(peticion.Nombre)) return "nombre";

        var cuit = (peticion.Cuit ?? string.Empty).Trim();
        if (cuit.Length == 0) return "cuit";

        if (!Enum.TryParse<TipoPersona>(peticion.Tipo, true, out _)) return "tipo";

        if (string.IsNullOrWhiteSpace(peticion.Telefono)) return "telefono";
        if (string.IsNullOrWhiteSpace(peticion.Email) || !peticion.Email.Contains('@')) return "email";

        return null;
    }
}
