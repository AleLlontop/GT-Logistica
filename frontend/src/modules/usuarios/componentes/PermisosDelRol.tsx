import type { RolConPermisos } from '../../../compartido/tipos'
import { Dialogo, DialogoAcciones } from '../../../compartido/ui/Dialogo'
import { Boton } from '../../../compartido/ui/Boton'

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
    <Dialogo titulo={`Permisos de ${rol.nombre}`} onCerrar={onCerrar}>
      <div className="mt-4 flex flex-col gap-5 text-[13.5px] text-ink">
        {rol.permisosPorModulo.length === 0 ? (
          <p role="status" className="m-0 text-faint">{MENSAJE_SIN_PERMISOS}</p>
        ) : (
          rol.permisosPorModulo.map((modulo) => (
            <div key={modulo.modulo} className="flex flex-col gap-2">
              <h3 className="m-0 font-bold tracking-tight text-ink">{modulo.modulo}</h3>
              <ul className="m-0 flex flex-col gap-1.5 pl-5 list-disc text-ink-soft">
                {modulo.permisos.map((permiso) => (
                  <li key={permiso.codigo}>{permiso.descripcion}</li>
                ))}
              </ul>
            </div>
          ))
        )}
      </div>

      <DialogoAcciones>
        <Boton type="button" variante="secundario" onClick={onCerrar}>
          Cerrar
        </Boton>
      </DialogoAcciones>
    </Dialogo>
  )
}
