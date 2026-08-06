using GT.Domain.Personas;

namespace GT.Application.Usuarios.Personas;

/// <summary>
/// Persona tal como la devuelve la API. Son los siete datos de FR-026 más el identificador y el
/// estado de la baja lógica.
/// </summary>
public record PersonaDto(
    int Id,
    string Nombre,
    string Apellido,
    string Dni,
    string Tipo,
    string Telefono,
    string Email,
    DateOnly FechaNacimiento,
    bool Activa)
{
    public static PersonaDto Desde(Persona persona) => new(
        persona.Id,
        persona.Nombre,
        persona.Apellido,
        persona.Dni,
        TipoIntegranteTexto.Desde(persona.Tipo),
        persona.Telefono,
        persona.Email,
        persona.FechaNacimiento,
        persona.Activa);
}

/// <summary>
/// Datos del alta y de la edición de una persona. Los siete de FR-026, ni uno más: la lista es
/// taxativa y no se amplía sin cambiar antes la spec.
/// </summary>
public record PersonaRequest(
    string? Nombre,
    string? Apellido,
    string? Dni,
    string? Tipo,
    string? Telefono,
    string? Email,
    DateOnly? FechaNacimiento);
