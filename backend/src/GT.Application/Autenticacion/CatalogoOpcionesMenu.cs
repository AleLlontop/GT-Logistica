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

        // Las entradas del Módulo 3 —Choferes, Transportistas y Tipos de documentación— se agregan
        // acá cuando sus pantallas existan, no antes: el menú no anuncia lo que todavía no está.
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
