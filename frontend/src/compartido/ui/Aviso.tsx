import type { ReactNode } from 'react'
import { cn } from './cn'
import { IconoEnRegla, IconoProximoAvencer, IconoVencido, IconoDocumento } from './iconos'

/**
 * Los mensajes que aparecen sin que la pantalla cambie: un guardado exitoso, un rechazo del
 * servidor, una advertencia que no bloquea, una nota.
 *
 * **`rol` es obligatorio y no tiene valor por defecto.** Esta primitiva *reemplaza* a los
 * `role="status"` y `role="alert"` escritos a mano en las pantallas, y no los elimina: la convención
 * [003] —todo resultado que aparece sin que la pantalla cambie se anuncia— es del sistema entero y
 * esta feature no la toca. Obligar a declararlo es lo que impide perderla por descuido.
 *
 * El tono y el ícono se suman a la palabra; ninguno de los dos reemplaza al texto (FR-037).
 */

type Tono = 'exito' | 'advertencia' | 'error' | 'nota'

const TONOS: Record<Tono, string> = {
  exito: 'bg-exito-fondo text-exito border-exito',
  advertencia: 'bg-advertencia-fondo text-advertencia border-advertencia',
  error: 'bg-error-fondo text-error border-error',
  nota: 'bg-superficie-hundida text-texto-suave border-borde-fuerte',
}

const ICONOS: Record<Tono, typeof IconoEnRegla> = {
  exito: IconoEnRegla,
  advertencia: IconoProximoAvencer,
  error: IconoVencido,
  nota: IconoDocumento,
}

interface Props {
  tono: Tono
  rol: 'status' | 'alert'
  children: ReactNode
  className?: string
}

export function Aviso({ tono, rol, children, className }: Props) {
  const Icono = ICONOS[tono]

  return (
    <div
      role={rol}
      className={cn(
        'flex items-start gap-2 rounded-medio border-l-4 px-4 py-3 text-sm font-medium',
        TONOS[tono],
        className,
      )}
    >
      <Icono aria-hidden="true" className="mt-0.5 size-4 shrink-0" />
      <div className="min-w-0">{children}</div>
    </div>
  )
}
