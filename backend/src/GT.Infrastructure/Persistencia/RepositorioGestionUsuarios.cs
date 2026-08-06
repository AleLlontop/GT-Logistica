using GT.Application.Usuarios;
using GT.Domain.Usuarios;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.Persistencia;

public class RepositorioGestionUsuarios(GtDbContext contexto)
    : IRepositorioGestionUsuarios, IRepositorioConsultaUsuarios, IRepositorioEscrituraUsuarios
{
    /// <summary>Con seguimiento, a diferencia de las consultas: acá se va a modificar la entidad.</summary>
    public Task<Usuario?> ObtenerParaEditarAsync(int id, CancellationToken cancelacion = default) =>
        contexto.Usuarios
            .Include(usuario => usuario.Roles)
            // La persona se incluye para que las respuestas de edición la reflejen: sin esto, la
            // navegación queda en null y el DTO informaría "sin persona asociada" sobre un usuario
            // que sí la tiene.
            .Include(usuario => usuario.Persona)
            .FirstOrDefaultAsync(usuario => usuario.Id == id, cancelacion);

    public Task GuardarCambiosAsync(CancellationToken cancelacion = default) =>
        contexto.SaveChangesAsync(cancelacion);

    /// <summary>
    /// Cuenta administradores activos **excluyendo** al usuario afectado. Ese detalle es lo que hace
    /// que la regla funcione cuando el afectado es el único administrador (FR-019, research §8).
    /// </summary>
    public Task<int> ContarAdministradoresActivosExcluyendoAsync(
        int idUsuarioExcluido,
        CancellationToken cancelacion = default) =>
        contexto.Usuarios.CountAsync(
            usuario => usuario.Id != idUsuarioExcluido &&
                       usuario.Estado == EstadoUsuario.Activo &&
                       usuario.Roles.Any(rol => rol.Codigo == CodigosRol.AdministradorSistema),
            cancelacion);

    public async Task<IReadOnlyList<Usuario>> BuscarAsync(
        FiltrosUsuarios filtros,
        CancellationToken cancelacion = default)
    {
        var consulta = contexto.Usuarios
            .Include(usuario => usuario.Roles)
            .AsNoTracking()
            .AsQueryable();

        // Los fragmentos ya llegan normalizados y se comparan contra las columnas normalizadas, para
        // no depender de la *collation* del servidor (research §4).
        if (filtros.Username is { } username)
        {
            consulta = consulta.Where(usuario => usuario.UsernameNormalizado.Contains(username));
        }

        if (filtros.Email is { } email)
        {
            consulta = consulta.Where(usuario => usuario.EmailNormalizado.Contains(email));
        }

        if (filtros.Rol is { } rol)
        {
            consulta = consulta.Where(usuario => usuario.Roles.Any(r => r.Codigo == rol));
        }

        if (filtros.Estado is { } estado)
        {
            consulta = consulta.Where(usuario => usuario.Estado == estado);
        }

        return await consulta
            .OrderBy(usuario => usuario.Username)
            .ToListAsync(cancelacion);
    }

    public Task<Usuario?> ObtenerDetalleAsync(int id, CancellationToken cancelacion = default) =>
        contexto.Usuarios
            .Include(usuario => usuario.Roles)
            .Include(usuario => usuario.Persona)
            .AsNoTracking()
            .FirstOrDefaultAsync(usuario => usuario.Id == id, cancelacion);

    public Task<bool> ExisteUsernameAsync(
        string usernameNormalizado,
        int? excluyendoUsuarioId = null,
        CancellationToken cancelacion = default) =>
        contexto.Usuarios.AnyAsync(
            usuario => usuario.UsernameNormalizado == usernameNormalizado &&
                       (excluyendoUsuarioId == null || usuario.Id != excluyendoUsuarioId),
            cancelacion);

    public Task<bool> ExisteEmailAsync(
        string emailNormalizado,
        int? excluyendoUsuarioId = null,
        CancellationToken cancelacion = default) =>
        contexto.Usuarios.AnyAsync(
            usuario => usuario.EmailNormalizado == emailNormalizado &&
                       (excluyendoUsuarioId == null || usuario.Id != excluyendoUsuarioId),
            cancelacion);

    public async Task<IReadOnlyList<Rol>> ObtenerRolesPorCodigoAsync(
        IReadOnlyList<string> codigos,
        CancellationToken cancelacion = default) =>
        await contexto.Roles
            .Where(rol => codigos.Contains(rol.Codigo))
            .ToListAsync(cancelacion);

    public async Task<ResultadoGuardado> AgregarAsync(
        Usuario usuario,
        CancellationToken cancelacion = default)
    {
        contexto.Usuarios.Add(usuario);

        try
        {
            await contexto.SaveChangesAsync(cancelacion);

            return ResultadoGuardado.Exitoso;
        }
        catch (DbUpdateException excepcion) when (EsViolacionDeIndiceUnico(excepcion))
        {
            // La entidad quedó marcada como agregada; si no se despega, el próximo SaveChanges del
            // mismo alcance vuelve a intentar insertarla.
            contexto.Entry(usuario).State = EntityState.Detached;

            return TraducirIndiceViolado(excepcion);
        }
    }

    /// <summary>
    /// 2601 y 2627 son los dos códigos con los que SQL Server informa una clave duplicada: el
    /// primero para un índice único, el segundo para una restricción de unicidad.
    /// </summary>
    private static bool EsViolacionDeIndiceUnico(DbUpdateException excepcion) =>
        excepcion.InnerException is SqlException { Number: 2601 or 2627 };

    /// <summary>
    /// Convierte la violación en el error de negocio que corresponde, mirando qué índice se violó.
    ///
    /// Es lo que permite que quien pierde una carrera de altas reciba exactamente el mismo mensaje
    /// que da la validación previa, en vez de un error técnico (research §3).
    /// </summary>
    private static ResultadoGuardado TraducirIndiceViolado(DbUpdateException excepcion)
    {
        var detalle = excepcion.InnerException?.Message ?? string.Empty;

        if (detalle.Contains("IX_Usuarios_UsernameNormalizado", StringComparison.Ordinal))
        {
            return ResultadoGuardado.UsernameDuplicado;
        }

        if (detalle.Contains("IX_Usuarios_EmailNormalizado", StringComparison.Ordinal))
        {
            return ResultadoGuardado.EmailDuplicado;
        }

        if (detalle.Contains("IX_Usuarios_PersonaId", StringComparison.Ordinal))
        {
            return ResultadoGuardado.PersonaYaVinculada;
        }

        // Un índice único que no conocemos: no hay traducción honesta posible, así que se deja
        // escapar para que salga como error inesperado en vez de inventar una causa.
        throw excepcion;
    }
}

/// <summary>Adapta el hasheador de infraestructura a la interfaz que consume la capa de aplicación.</summary>
public class HasheadorPasswordApp(Seguridad.IHasheadorPassword hasheador) : IHasheadorPasswordApp
{
    public string Hashear(string password) => hasheador.Hashear(password);
}
