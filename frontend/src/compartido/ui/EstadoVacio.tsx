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
  vacio: 'text-texto-suave',
  sinCoincidencias: 'text-texto-suave',
  cargando: 'text-texto-tenue',
  error: 'text-error',
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
        'flex flex-col items-center gap-3 rounded-medio border border-dashed border-borde',
        'bg-superficie px-6 py-12 text-center',
        className,
      )}
    >
      <Icono aria-hidden="true" className={cn('size-8', TONOS[caso])} />
      <p className={cn('max-w-prose text-sm', TONOS[caso])}>{children}</p>
      {accion}
    </div>
  )
}
