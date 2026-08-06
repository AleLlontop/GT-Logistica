import type { RolConPermisos } from '../../../compartido/tipos'

const MENSAJE_SIN_PERMISOS = 'Este rol todavía no habilita funcionalidades implementadas.'

interface Props {
  rol: RolConPermisos
  onCerrar: () => void
}

/**
 * Permisos de un rol, agrupados por módulo y **en modo lectura** (FR-010).
 *
 * Sin casillas ni botones de edición: este módulo no crea, edita ni elimina roles ni permisos.
 *
 * Que un rol venga sin permisos es lo esperado mientras los módulos que se los otorgan no estén
 * implementados. Se dice con texto, no con una lista vacía sin explicación.
 */
export function PermisosDelRol({ rol, onCerrar }: Props) {
  return (
    <section aria-labelledby="titulo-permisos">
      <h2 id="titulo-permisos">Permisos de {rol.nombre}</h2>

      {rol.permisosPorModulo.length === 0 ? (
        <p role="status">{MENSAJE_SIN_PERMISOS}</p>
      ) : (
        rol.permisosPorModulo.map((modulo) => (
          <div key={modulo.modulo}>
            <h3>{modulo.modulo}</h3>
            <ul>
              {modulo.permisos.map((permiso) => (
                <li key={permiso.codigo}>{permiso.descripcion}</li>
              ))}
            </ul>
          </div>
        ))
      )}

      <button type="button" onClick={onCerrar}>
        Cerrar
      </button>
    </section>
  )
}
