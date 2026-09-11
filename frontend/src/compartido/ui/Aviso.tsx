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
 *
 * **La región vive aunque el mensaje no.** Un `role` no anuncia nada si el nodo que lo lleva entra
 * al DOM junto con su texto: el lector de pantalla necesita la región ya presente para notar que
 * algo le entró después. Por eso el aviso no se monta condicionalmente desde afuera —
 * `{condicion && <Aviso …>}` lo haría aparecer y desaparecer entero— sino que **siempre se dibuja y
 * recibe el mensaje como hijo**; sin hijos se reduce a un contenedor `sr-only`, que sigue en el
 * árbol accesible y, por ser absoluto, no ocupa un hueco del `gap` del formulario. Cuando el mensaje
 * llega, React reusa ese mismo nodo y el anuncio sale.
 *
 * El `role` queda sobre el elemento que contiene el texto y no sobre una tarjeta que lo enmarque.
 */

type Tono = 'exito' | 'advertencia' | 'error' | 'nota'

const TONOS: Record<Tono, string> = {
  exito: 'bg-estado-rendido-bg text-estado-rendido',
  advertencia: 'bg-estado-pendiente-bg text-estado-pendiente',
  error: 'bg-danger-bg text-danger-text',
  nota: 'bg-surface-mute text-ink-soft',
}

/** El chip del ícono: el mismo tono, un punto más saturado, para que se lea como una pieza. */
const CHIPS: Record<Tono, string> = {
  exito: 'bg-estado-rendido/10',
  advertencia: 'bg-estado-pendiente/10',
  error: 'bg-danger/10',
  nota: 'bg-white/70',
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

  // `false` es lo que deja un `{condicion && …}` del lado del que llama, y `null`/`undefined` lo que
  // deja un ternario sin rama. Los tres son "todavía no hay mensaje".
  if (children === false || children === null || children === undefined) {
    return <div role={rol} className="sr-only" />
  }

  return (
    <div
      role={rol}
      className={cn(
        'flex items-start gap-3.5 rounded-card border border-line px-[18px] py-4',
        'text-[13px] leading-5 font-medium',
        TONOS[tono],
        className,
      )}
    >
      <span
        className={cn(
          'flex size-8 shrink-0 items-center justify-center rounded-chip',
          CHIPS[tono],
        )}
      >
        <Icono aria-hidden="true" className="size-3.5" />
      </span>
      <div className="min-w-0 self-center">{children}</div>
    </div>
  )
}
