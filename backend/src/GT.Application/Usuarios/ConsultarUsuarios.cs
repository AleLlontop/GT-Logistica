using GT.Domain.Autenticacion;
using GT.Domain.Usuarios;

namespace GT.Application.Usuarios;

/// <summary>
/// Los cuatro filtros del listado (FR-011). Se combinan con "y": el resultado cumple todas las
/// condiciones enviadas.
/// </summary>
/// <param name="Username">Fragmento, coincidencia parcial sin distinguir mayúsculas.</param>
/// <param name="Email">Fragmento, coincidencia parcial sin distinguir mayúsculas.</param>
/// <param name="Rol">Código de rol, igualdad exacta.</param>
/// <param name="Estado">Estado, igualdad exacta.</param>
public record FiltrosUsuarios(
    string? Username = null,
    string? Email = null,
    string? Rol = null,
    EstadoUsuario? Estado = null);

/// <summary>Consulta de usuarios que necesita el listado y el detalle. La implementa infraestructura.</summary>
public interface IRepositorioConsultaUsuarios
{
    Task<IReadOnlyList<Usuario>> BuscarAsync(
        FiltrosUsuarios filtros,
        CancellationToken cancelacion = default);

    /// <summary>Trae el usuario con sus roles y su persona, o <c>null</c> si no existe.</summary>
    Task<Usuario?> ObtenerDetalleAsync(int id, CancellationToken cancelacion = default);
}

/// <summary>
/// Consulta de usuarios (User Story 2).
///
/// Los filtros de username y email buscan sobre las columnas <b>normalizadas</b>, con el término
/// también normalizado. No es un detalle de implementación: filtrar sobre la columna original
/// funcionaría sólo porque SQL Server suele venir con una *collation* insensible a mayúsculas, y eso
/// deja el cumplimiento de FR-011 atado a una configuración del servidor que nadie declaró
/// (research §4).
/// </summary>
public class ConsultarUsuarios(IRepositorioConsultaUsuarios repositorio)
{
    public async Task<IReadOnlyList<UsuarioListado>> EjecutarAsync(
        string? username,
        string? email,
        string? rol,
        string? estado,
        CancellationToken cancelacion = default)
    {
        var filtros = new FiltrosUsuarios(
            NormalizarFragmento(username, NormalizadorUsername.Normalizar),
            NormalizarFragmento(email, NormalizadorEmail.Normalizar),
            string.IsNullOrWhiteSpace(rol) ? null : rol.Trim(),
            EstadoUsuarioTexto.Interpretar(estado));

        var usuarios = await repositorio.BuscarAsync(filtros, cancelacion);

        return [.. usuarios.Select(UsuarioListado.Desde)];
    }

    /// <returns><c>null</c> si no existe.</returns>
    public async Task<UsuarioDetalle?> ObtenerDetalleAsync(
        int id,
        CancellationToken cancelacion = default)
    {
        var usuario = await repositorio.ObtenerDetalleAsync(id, cancelacion);

        return usuario is null ? null : UsuarioDetalle.Desde(usuario);
    }

    /// <summary>
    /// Un filtro vacío no filtra. Uno con texto se normaliza igual que la columna contra la que se
    /// va a comparar.
    /// </summary>
    private static string? NormalizarFragmento(string? valor, Func<string?, string> normalizar) =>
        string.IsNullOrWhiteSpace(valor) ? null : normalizar(valor);
}
