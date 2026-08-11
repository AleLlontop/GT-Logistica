using GT.Domain.Usuarios;

namespace GT.Application.Autenticacion;

/// <summary>
/// Traducción de permisos efectivos a opciones de menú (FR-020).
///
/// El servidor es la única fuente de verdad del menú: el frontend dibuja lo que recibe y no tiene
/// lógica propia de permisos (research §8). Esto evita duplicar en TypeScript el mapeo
/// permiso → pantalla que ya existe acá.
///
/// Sólo figuran opciones de funcionalidades **ya implementadas**: el menú no anuncia módulos que
/// todavía no existen, y cada módulo nuevo agrega su entrada cuando se construye.
/// </summary>
public static class CatalogoOpcionesMenu
{
    private static readonly (string Permiso, OpcionMenuDto Opcion)[] Catalogo =
    [
        (CodigosPermiso.UsuariosGestionar,
            new OpcionMenuDto("usuarios", "Gestión de usuarios", "/usuarios")),

        // El padrón de personas es parte del Módulo 2 y comparte su restricción de acceso: no lleva
        // un permiso propio (FR-007, research §7).
        (CodigosPermiso.UsuariosGestionar,
            new OpcionMenuDto("personas", "Personas", "/personas")),

        // Módulo 3. Las tres entradas van atadas al mismo permiso `choferes.gestionar`, que otorgan
        // Tráfico y Administrador del sistema: es el primer módulo cuyo acceso no es exclusivo del
        // administrador (FR-027, contracts/README.md).
        (CodigosPermiso.ChoferesGestionar,
            new OpcionMenuDto("choferes", "Choferes", "/choferes")),
        (CodigosPermiso.ChoferesGestionar,
            new OpcionMenuDto("transportistas", "Transportistas", "/transportistas")),
        (CodigosPermiso.ChoferesGestionar,
            new OpcionMenuDto("tipos-documentacion", "Tipos de documentación", "/tipos-documentacion")),

        // Módulo 4. Las dos entradas van atadas a permisos **distintos**: es el primer módulo que
        // distingue niveles de acceso adentro, y el catálogo de tipos de vehículo es sólo del
        // administrador. Tráfico ve *Flota* y no ve *Tipos de vehículo* (FR-039, research §7).
        (CodigosPermiso.FlotaGestionar,
            new OpcionMenuDto("flota", "Flota", "/flota")),
        (CodigosPermiso.FlotaTiposGestionar,
            new OpcionMenuDto("tipos-vehiculo", "Tipos de vehículo", "/tipos-vehiculo")),

        // Módulo 5. Las tres entradas van atadas a `viajes.consultar` —el permiso **de lectura**— y
        // no a `viajes.gestionar`: las tres pantallas se pueden mirar sin poder tocar nada, y quien
        // sólo consulta no ve adentro ningún botón de alta, edición, asignación ni cambio de estado
        // (FR-050, research §10).
        (CodigosPermiso.ViajesConsultar, new OpcionMenuDto("viajes", "Viajes", "/viajes")),
        (CodigosPermiso.ViajesConsultar, new OpcionMenuDto("clientes", "Clientes", "/clientes")),
        (CodigosPermiso.ViajesConsultar, new OpcionMenuDto("totales", "Totales", "/viajes/totales")),
    ];

    /// <summary>
    /// Devuelve las opciones que los permisos del usuario autorizan. Puede venir vacía: un usuario
    /// cuyos roles todavía no habilitan nada implementado igual inicia sesión y llega a la pantalla
    /// de inicio, con el menú sin opciones.
    /// </summary>
    public static IEnumerable<OpcionMenuDto> Autorizadas(IEnumerable<string> permisosEfectivos)
    {
        var permisos = permisosEfectivos.ToHashSet();

        return Catalogo
            .Where(entrada => permisos.Contains(entrada.Permiso))
            .Select(entrada => entrada.Opcion);
    }
}
