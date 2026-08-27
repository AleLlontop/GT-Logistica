import type { ReactNode } from 'react'
import { cn } from './cn'
import { IconoBuscar, IconoDocumento, IconoPendiente, IconoVencido } from './iconos'

/**
 * Lo que ocupa el lugar de la tabla cuando no hay filas que mostrar.
 *
 * Los cuatro casos tienen tratamiento propio y se distinguen entre sí (FR-023). En particular
 * **"todavía no hay ninguno" y "tu filtro no encontró nada" son situaciones distintas** que llevan a
 * acciones distintas, y las specs de los módulos anteriores les escribieron textos distintos a
 * propósito. Por eso el texto llega desde afuera: acá se decide cómo se ve, no qué dice.
 */

type Caso = 'vacio' | 'sinCoincidencias' | 'cargando' | 'error'

const ICONOS: Record<Caso, typeof IconoDocumento> = {
  vacio: IconoDocumento,
  sinCoincidencias: IconoBuscar,
  cargando: IconoPendiente,
  error: IconoVencido,
}

const TONOS: Record<Caso, string> = {
  vacio: 'text-ink-soft',
  sinCoincidencias: 'text-ink-soft',
  cargando: 'text-faint',
  error: 'text-danger-text',
}

/** El chip del ícono. `dim` acá es relleno decorativo, no texto (FR-007a). */
const CHIPS: Record<Caso, string> = {
  vacio: 'bg-surface-mute text-dim',
  sinCoincidencias: 'bg-surface-mute text-dim',
  cargando: 'bg-surface-mute text-dim',
  error: 'bg-danger-bg text-danger',
}

interface Props {
  caso: Caso
  children: ReactNode
  /** Una acción para salir de la situación: *Emitir la primera factura*, *Limpiar los filtros*. */
  accion?: ReactNode
  className?: string
}

export function EstadoVacio({ caso, children, accion, className }: Props) {
  const Icono = ICONOS[caso]

  return (
    <div
      role={caso === 'error' ? 'alert' : 'status'}
      className={cn(
        'flex flex-col items-center gap-3.5 rounded-card border border-line',
        'bg-surface px-6 py-12 text-center',
        className,
      )}
    >
      <span
        className={cn(
          'flex size-[38px] items-center justify-center rounded-chip',
          CHIPS[caso],
        )}
      >
        <Icono aria-hidden="true" className="size-4" />
      </span>
      <p className={cn('m-0 max-w-prose text-[13px] leading-[19px]', TONOS[caso])}>{children}</p>
      {accion}
    </div>
  )
}
