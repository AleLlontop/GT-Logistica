using GT.Domain.Choferes;

namespace GT.Application.Choferes.Transportistas;

public enum ErrorTransportista
{
    Ninguno,
    NoEncontrado,
    DatosInvalidos,
    CuitDuplicado,

    /// <summary>
    /// El transportista tiene dependientes activos. Desde el Módulo 4 cuentan <b>choferes y
    /// vehículos</b>, y el mensaje informa las dos cantidades (FR-008d).
    /// </summary>
    ConChoferes,
}

public record ResultadoTransportista(ErrorTransportista Error, TransportistaDto? Transportista, string? Campo = null)
{
    public bool Exitoso => Error is ErrorTransportista.Ninguno;

    public int? CantidadChoferes { get; init; }

    /// <summary>
    /// Vehículos activos que dependen del transportista. Desde el Módulo 4 la baja los cuenta también,
    /// y el mensaje informa <b>las dos</b> cantidades por separado (FR-008d, SC-008).
    /// </summary>
    public int? CantidadVehiculos { get; init; }
}

public record TransportistaRequest(
    string? Nombre,
    string? Cuit,
    string? Tipo,
    string? Telefono,
    string? Email);

/// <summary>
/// Un transportista con la cantidad de <b>dependientes activos</b> —choferes y vehículos—, que es lo
/// que impide su baja (FR-010, y desde el Módulo 4 también FR-008d) y lo que el listado muestra.
///
/// Las dos cantidades se resuelven en la misma consulta: una fila con dos números, no dos colecciones
/// traídas a memoria (research §8).
/// </summary>
public record TransportistaConDependenciasActivas(
    Transportista Transportista,
    int ChoferesActivos,
    int VehiculosActivos);

/// <param name="VehiculosActivos">
/// Cuántos vehículos activos pertenecen al transportista (Módulo 4, FR-008d). El listado lo muestra
/// junto a los choferes: es lo que explica por qué algunos no se pueden dar de baja.
/// </param>
public record TransportistaDto(
    int Id,
    string Nombre,
    string Cuit,
    string Tipo,
    string Telefono,
    string Email,
    bool Activo,
    int ChoferesActivos,
    int VehiculosActivos)
{
    public static TransportistaDto Desde(
        Transportista transportista,
        int choferesActivos = 0,
        int vehiculosActivos = 0) => new(
        transportista.Id,
        transportista.Nombre,
        transportista.Cuit,
        transportista.Tipo.ToString().ToLowerInvariant(),
        transportista.Telefono,
        transportista.Email,
        transportista.Activo,
        choferesActivos,
        vehiculosActivos);

    public static TransportistaDto Desde(TransportistaConDependenciasActivas fila) =>
        Desde(fila.Transportista, fila.ChoferesActivos, fila.VehiculosActivos);
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
