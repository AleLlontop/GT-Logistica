using GT.Domain.Personas;

namespace GT.Application.Usuarios.Personas;

/// <summary>
/// Acceso al padrón de personas. Lo implementa la capa de infraestructura, igual que
/// <c>IRepositorioUsuarios</c> del Módulo 1: <c>GT.Application</c> no puede referenciar
/// <c>GT.Infrastructure</c> sin invertir la dirección de las capas.
/// </summary>
public interface IRepositorioPersonas
{
    /// <param name="texto">
    /// Fragmento a buscar en nombre, apellido o DNI. Coincidencia parcial y sin distinguir
    /// mayúsculas. <c>null</c> o vacío devuelve todo el padrón.
    /// </param>
    /// <param name="soloActivas">
    /// <c>true</c> deja afuera a las dadas de baja: es lo que consume el selector del formulario de
    /// usuario (FR-023).
    /// </param>
    Task<IReadOnlyList<Persona>> BuscarAsync(
        string? texto,
        bool soloActivas,
        CancellationToken cancelacion = default);

    Task<Persona?> ObtenerPorIdAsync(int id, CancellationToken cancelacion = default);

    /// <summary>Existe y está activa: las dos condiciones que FR-023 exige para poder asociarla.</summary>
    Task<bool> EstaDisponibleAsync(int id, CancellationToken cancelacion = default);

    Task<bool> ExisteDniAsync(
        string dni,
        int? excluyendoPersonaId = null,
        CancellationToken cancelacion = default);

    Task AgregarAsync(Persona persona, CancellationToken cancelacion = default);

    Task GuardarCambiosAsync(CancellationToken cancelacion = default);

    /// <summary>
    /// Username del usuario que tiene asociada a esta persona, o <c>null</c> si no la tiene ninguno.
    /// Se usa para poder nombrarlo en los mensajes de FR-008 y FR-028, sin importar su estado.
    /// </summary>
    Task<string?> UsernameDelUsuarioVinculadoAsync(
        int personaId,
        CancellationToken cancelacion = default);

    /// <summary>
    /// <c>true</c> si esta persona está registrada como chofer (Módulo 3), activo o inactivo.
    ///
    /// Darla de baja dejaría un chofer apuntando a una persona inactiva, así que la baja se rechaza
    /// igual que cuando tiene un usuario asociado.
    /// </summary>
    Task<bool> EsChoferAsync(int personaId, CancellationToken cancelacion = default);
}

/// <summary>
/// Consulta del padrón de personas (FR-023, FR-025).
///
/// Es la única parte del padrón que vive en la fase base del módulo y no en su historia de usuario:
/// el selector de persona del formulario de alta la necesita antes de que exista el ABM completo.
/// </summary>
public class ConsultarPersonas(IRepositorioPersonas repositorio)
{
    public async Task<IReadOnlyList<PersonaDto>> EjecutarAsync(
        string? texto,
        bool soloActivas,
        CancellationToken cancelacion = default)
    {
        var personas = await repositorio.BuscarAsync(texto, soloActivas, cancelacion);

        return [.. personas.Select(PersonaDto.Desde)];
    }

    /// <returns><c>null</c> si no existe.</returns>
    public async Task<PersonaDto?> ObtenerAsync(int id, CancellationToken cancelacion = default)
    {
        var persona = await repositorio.ObtenerPorIdAsync(id, cancelacion);

        return persona is null ? null : PersonaDto.Desde(persona);
    }
}
