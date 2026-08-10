namespace GT.Domain.Usuarios;

/// <summary>
/// Agrupación fija de permisos asignada a uno o más usuarios. El catálogo es fijo y queda cargado
/// en la instalación (FR-019); la asignación de roles a usuarios es responsabilidad del Módulo 2.
/// </summary>
public class Rol
{
    public int Id { get; set; }

    /// <summary>Identificador estable para el código, por ejemplo <c>administrador_sistema</c>.</summary>
    public required string Codigo { get; set; }

    /// <summary>Nombre visible, en español.</summary>
    public required string Nombre { get; set; }

    public ICollection<Permiso> Permisos { get; set; } = [];

    public ICollection<Usuario> Usuarios { get; set; } = [];
}

/// <summary>Códigos de los cuatro roles del sistema. Fijos en esta versión.</summary>
public static class CodigosRol
{
    public const string Trafico = "trafico";
    public const string Administracion = "administracion";
    public const string Gerencia = "gerencia";
    public const string AdministradorSistema = "administrador_sistema";
}

/// <summary>Códigos de los permisos del sistema. Cada módulo agrega el suyo cuando se construye.</summary>
public static class CodigosPermiso
{
    public const string UsuariosGestionar = "usuarios.gestionar";

    /// <summary>
    /// Módulo 3. A diferencia del anterior, no es exclusivo del administrador: lo otorgan
    /// *Tráfico* y *Administrador del sistema* (FR-027). Es un único permiso para todo el módulo,
    /// porque la spec no distingue niveles de acceso dentro de él.
    /// </summary>
    public const string ChoferesGestionar = "choferes.gestionar";

    /// <summary>
    /// Módulo 4: vehículos, su documentación, el panel de vencimientos y la descarga de adjuntos. Lo
    /// otorgan *Tráfico* y *Administrador del sistema* (FR-039).
    /// </summary>
    public const string FlotaGestionar = "flota.gestionar";

    /// <summary>
    /// Módulo 4: sólo el ABM del catálogo de tipos de vehículo, y lo otorga únicamente
    /// *Administrador del sistema* (FR-039).
    ///
    /// Es la primera vez que un módulo distingue niveles de acceso <b>adentro</b>. Se resuelve con
    /// dos permisos y no con un chequeo de rol en el endpoint, porque la convención del Módulo 1 es
    /// autorizar por permiso y nunca por rol (research §7).
    /// </summary>
    public const string FlotaTiposGestionar = "flota.tipos.gestionar";
}
