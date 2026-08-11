using GT.Domain.Usuarios;
using GT.Infrastructure.Persistencia;
using GT.Infrastructure.Seguridad;
using Microsoft.EntityFrameworkCore;

namespace GT.Infrastructure.DatosIniciales;

/// <summary>
/// Datos iniciales del sistema (FR-019): el catálogo fijo de roles y permisos, y el usuario
/// administrador inicial.
///
/// Es idempotente: si el usuario <c>admin</c> ya existe, no se toca ni se le pisa la contraseña.
/// No crea ninguna otra cuenta, ni de ejemplo ni de prueba: el resto de los usuarios se dan de alta
/// desde el Módulo 2.
/// </summary>
public class SembradorInicial(GtDbContext contexto, IHasheadorPassword hasheador)
{
    public const string UsernameAdministrador = "admin";
    public const string EmailAdministradorInicial = "admin@gtlogistica.local";
    public const string VariablePasswordInicial = "GT_ADMIN_PASSWORD_INICIAL";

    private static readonly (string Codigo, string Nombre)[] RolesDelSistema =
    [
        (CodigosRol.Trafico, "Tráfico"),
        (CodigosRol.Administracion, "Administración de la empresa"),
        (CodigosRol.Gerencia, "Gerencia"),
        (CodigosRol.AdministradorSistema, "Administrador del sistema"),
    ];

    private static readonly (string Codigo, string Modulo, string Descripcion)[] PermisosDelSistema =
    [
        (CodigosPermiso.UsuariosGestionar, "Usuarios",
            "Crear, consultar, modificar y dar de baja usuarios y sus roles"),

        (CodigosPermiso.ChoferesGestionar, "Choferes",
            "Gestionar transportistas, choferes y su documentación"),

        (CodigosPermiso.FlotaGestionar, "Flota",
            "Gestionar vehículos, su documentación y el panel de vencimientos"),

        (CodigosPermiso.FlotaTiposGestionar, "Flota",
            "Mantener el catálogo de tipos de vehículo"),

        (CodigosPermiso.ViajesGestionar, "Viajes",
            "Registrar viajes y clientes, asignar chofer y vehículo, y cambiar el estado del viaje"),

        (CodigosPermiso.ViajesConsultar, "Viajes",
            "Consultar viajes, clientes y totales por período"),
    ];

    /// <summary>
    /// Qué permisos otorga cada rol. Cada módulo agrega el suyo cuando se construye.
    ///
    /// El Módulo 3 es el primero que habilita algo para un rol que no es el administrador: *Tráfico*
    /// recibe `choferes.gestionar` y ningún permiso del Módulo 2 (FR-027).
    ///
    /// El Módulo 4 es el primero que reparte **dos** permisos del mismo módulo de forma distinta:
    /// Tráfico gestiona la flota pero no el catálogo de tipos de vehículo, que es sólo del
    /// administrador (Módulo 4, FR-039, research §7).
    ///
    /// El Módulo 5 es el primero que le da algo a *Administración de la empresa* y a *Gerencia*:
    /// `viajes.consultar` lo reciben **los cuatro roles**, porque mirar el listado, la ficha y los
    /// totales no exige poder operar. `viajes.gestionar` sigue el reparto de siempre, Tráfico y
    /// administrador (Módulo 5, FR-051, research §10).
    /// </summary>
    private static readonly Dictionary<string, string[]> PermisosPorRol = new()
    {
        [CodigosRol.AdministradorSistema] =
        [
            CodigosPermiso.UsuariosGestionar,
            CodigosPermiso.ChoferesGestionar,
            CodigosPermiso.FlotaGestionar,
            CodigosPermiso.FlotaTiposGestionar,
            CodigosPermiso.ViajesGestionar,
            CodigosPermiso.ViajesConsultar,
        ],

        [CodigosRol.Trafico] =
        [
            CodigosPermiso.ChoferesGestionar,
            CodigosPermiso.FlotaGestionar,
            CodigosPermiso.ViajesGestionar,
            CodigosPermiso.ViajesConsultar,
        ],

        [CodigosRol.Administracion] = [CodigosPermiso.ViajesConsultar],

        [CodigosRol.Gerencia] = [CodigosPermiso.ViajesConsultar],
    };

    /// <param name="passwordInicial">
    /// Valor de <c>GT_ADMIN_PASSWORD_INICIAL</c>. Sólo hace falta cuando el usuario administrador
    /// todavía no existe; una vez sembrado, puede venir vacío.
    /// </param>
    public async Task SembrarAsync(string? passwordInicial, CancellationToken cancelacion = default)
    {
        await SembrarPermisosAsync(cancelacion);
        await SembrarRolesAsync(cancelacion);
        await SembrarAdministradorInicialAsync(passwordInicial, cancelacion);
    }

    private async Task SembrarPermisosAsync(CancellationToken cancelacion)
    {
        foreach (var (codigo, modulo, descripcion) in PermisosDelSistema)
        {
            if (await contexto.Permisos.AnyAsync(p => p.Codigo == codigo, cancelacion))
            {
                continue;
            }

            contexto.Permisos.Add(new Permiso
            {
                Codigo = codigo,
                Modulo = modulo,
                Descripcion = descripcion,
            });
        }

        await contexto.SaveChangesAsync(cancelacion);
    }

    private async Task SembrarRolesAsync(CancellationToken cancelacion)
    {
        var permisos = await contexto.Permisos.ToDictionaryAsync(p => p.Codigo, cancelacion);

        foreach (var (codigo, nombre) in RolesDelSistema)
        {
            var rol = await contexto.Roles
                .Include(r => r.Permisos)
                .FirstOrDefaultAsync(r => r.Codigo == codigo, cancelacion);

            if (rol is null)
            {
                rol = new Rol { Codigo = codigo, Nombre = nombre };
                contexto.Roles.Add(rol);
            }

            var codigosEsperados = PermisosPorRol.GetValueOrDefault(codigo, []);

            foreach (var codigoPermiso in codigosEsperados)
            {
                var yaLoTiene = rol.Permisos.Any(p => p.Codigo == codigoPermiso);

                if (!yaLoTiene && permisos.TryGetValue(codigoPermiso, out var permiso))
                {
                    rol.Permisos.Add(permiso);
                }
            }
        }

        await contexto.SaveChangesAsync(cancelacion);
    }

    private async Task SembrarAdministradorInicialAsync(
        string? passwordInicial,
        CancellationToken cancelacion)
    {
        var normalizado = UsernameAdministrador.ToUpperInvariant();

        var yaExiste = await contexto.Usuarios
            .AnyAsync(u => u.UsernameNormalizado == normalizado, cancelacion);

        if (yaExiste)
        {
            // Idempotente: no se pisa la contraseña de un administrador ya creado, y la variable de
            // entorno deja de ser necesaria a partir de acá (research §6).
            return;
        }

        if (string.IsNullOrWhiteSpace(passwordInicial))
        {
            throw new InvalidOperationException(
                $"Falta la variable de entorno {VariablePasswordInicial}, necesaria para crear el " +
                "usuario administrador inicial. Copiá .env.ejemplo a .env, definila y volvé a " +
                "levantar el sistema. Una vez creado el administrador, la variable deja de hacer " +
                "falta.");
        }

        var rolAdministrador = await contexto.Roles
            .FirstAsync(r => r.Codigo == CodigosRol.AdministradorSistema, cancelacion);

        var ahora = DateTime.UtcNow;

        var administrador = new Usuario
        {
            Username = UsernameAdministrador,
            UsernameNormalizado = normalizado,
            // Dirección de arranque, no real: el dominio `.local` no existe, así que no se le puede
            // mandar un correo por accidente. El responsable de sistemas la corrige desde el Módulo 2
            // apenas entra (research §5). Es el mismo valor con el que la migración rellena la fila
            // del `admin` que ya existía antes de este módulo.
            Email = EmailAdministradorInicial,
            EmailNormalizado = NormalizadorEmail.Normalizar(EmailAdministradorInicial),
            PasswordHash = hasheador.Hashear(passwordInicial),
            Estado = EstadoUsuario.Activo,
            FechaAlta = ahora,
            UltimoAcceso = null,
            PasswordTemporalGeneradaEn = null,
            PasswordActualizadaEn = ahora,
        };

        administrador.Roles.Add(rolAdministrador);
        contexto.Usuarios.Add(administrador);

        await contexto.SaveChangesAsync(cancelacion);
    }
}
