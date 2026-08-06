using GT.Domain.Usuarios;

namespace GT.Application.Usuarios;

/// <summary>Motivo por el que no se pudo guardar la selección de roles.</summary>
public enum ErrorRoles
{
    Ninguno,
    NoEncontrado,
    SinRoles,
    RolInexistente,
    UltimoAdministrador,
}

public record ResultadoRoles(ErrorRoles Error, UsuarioDetalle? Usuario)
{
    public bool Exitoso => Error is ErrorRoles.Ninguno;
}

/// <summary>Lectura del catálogo de roles y permisos. La implementa infraestructura.</summary>
public interface IRepositorioRoles
{
    /// <summary>Los cuatro roles del sistema con sus permisos.</summary>
    Task<IReadOnlyList<Rol>> ObtenerTodosConPermisosAsync(CancellationToken cancelacion = default);
}

/// <summary>
/// Asignación de roles a un usuario (User Story 4).
///
/// Es un <b>reemplazo</b>, no un agregado: los roles quedan exactamente como se enviaron, se agregan
/// los que faltan y se quitan los que no vinieron (FR-018).
///
/// Quitar el rol <i>Administrador del sistema</i> es uno de los tres caminos que pueden dejar al
/// sistema sin administradores, así que pasa por <see cref="ProteccionUltimoAdministrador"/>
/// (FR-019).
/// </summary>
public class AsignarRoles(
    IRepositorioEscrituraUsuarios repositorio,
    IRepositorioGestionUsuarios gestion)
{
    public async Task<ResultadoRoles> EjecutarAsync(
        int idUsuario,
        AsignarRolesRequest peticion,
        CancellationToken cancelacion = default)
    {
        var usuario = await repositorio.ObtenerParaEditarAsync(idUsuario, cancelacion);

        if (usuario is null)
        {
            return new ResultadoRoles(ErrorRoles.NoEncontrado, null);
        }

        var codigos = (peticion.Roles ?? []).Distinct().ToList();

        // FR-001: una lista vacía se rechaza y el usuario conserva los que tenía.
        if (codigos.Count == 0)
        {
            return new ResultadoRoles(ErrorRoles.SinRoles, null);
        }

        var roles = await gestion.ObtenerRolesPorCodigoAsync(codigos, cancelacion);

        if (roles.Count != codigos.Count)
        {
            return new ResultadoRoles(ErrorRoles.RolInexistente, null);
        }

        // ── FR-019: no dejar al sistema sin administradores activos ────────────────────────────
        var eraAdministrador = usuario.Roles.Any(rol =>
            rol.Codigo == CodigosRol.AdministradorSistema);

        var seguiraSiendolo = codigos.Contains(CodigosRol.AdministradorSistema);

        if (eraAdministrador && !seguiraSiendolo)
        {
            var restantes = await repositorio.ContarAdministradoresActivosExcluyendoAsync(
                idUsuario,
                cancelacion);

            var permitido = ProteccionUltimoAdministrador.SePuedeEjecutar(
                usuario.Estado == EstadoUsuario.Activo,
                restantes,
                OperacionSobreAdministrador.QuitarRolAdministrador);

            if (!permitido)
            {
                return new ResultadoRoles(ErrorRoles.UltimoAdministrador, null);
            }
        }

        // El reemplazo: se vacía y se rearma con lo que llegó. Con cuatro roles fijos, calcular el
        // diferencial no aportaría nada y complicaría la lectura.
        usuario.Roles.Clear();

        foreach (var rol in roles)
        {
            usuario.Roles.Add(rol);
        }

        await repositorio.GuardarCambiosAsync(cancelacion);

        return new ResultadoRoles(ErrorRoles.Ninguno, UsuarioDetalle.Desde(usuario));
    }
}

/// <summary>
/// Consulta del catálogo de roles con sus permisos, agrupados por módulo (FR-010).
///
/// Sólo lectura: este módulo no crea, edita ni elimina roles ni permisos. Un rol puede venir con la
/// lista vacía, y es lo esperado mientras los módulos que otorgan esos permisos no existan.
/// </summary>
public class ConsultarRoles(IRepositorioRoles repositorio)
{
    public async Task<IReadOnlyList<RolConPermisos>> EjecutarAsync(
        CancellationToken cancelacion = default)
    {
        var roles = await repositorio.ObtenerTodosConPermisosAsync(cancelacion);

        return
        [
            .. roles.Select(rol => new RolConPermisos(
                rol.Codigo,
                rol.Nombre,
                [
                    .. rol.Permisos
                        .GroupBy(permiso => permiso.Modulo)
                        .OrderBy(grupo => grupo.Key)
                        .Select(grupo => new PermisosDeModulo(
                            grupo.Key,
                            [
                                .. grupo
                                    .OrderBy(permiso => permiso.Codigo)
                                    .Select(permiso => new PermisoResumen(
                                        permiso.Codigo,
                                        permiso.Descripcion)),
                            ])),
                ])),
        ];
    }
}
