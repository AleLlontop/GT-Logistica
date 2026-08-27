import type { ReactNode } from 'react'
import { cn } from './cn'

/** La leyenda de obligatorios del sistema. Un solo texto para los 19 formularios (FR-027). */
export const LEYENDA_DE_OBLIGATORIOS = 'Los campos con * son obligatorios'

interface Props {
  /**
   * `viewport` para un formulario de página completa: la barra queda **fija al pie de la ventana**,
   * no al final del desplazamiento. En un formulario largo, que la acción que completa la tarea
   * dependa de dónde estés parado es lo que hace que alguien lo abandone a la mitad.
   *
   * `contenedor` para un formulario dentro de un diálogo y para las dos pantallas sin sesión: ahí la
   * barra va al pie de su propia superficie, porque el diálogo ya está anclado y la pantalla de
   * ingreso entra entera sin desplazar.
   *
   * **Obligatoria**: es la decisión que FR-030 distingue y no tiene un valor por defecto correcto.
   */
  anclaje: 'viewport' | 'contenedor'
  /** La leyenda de obligatorios, a la izquierda. */
  leyenda?: ReactNode
  /** Las acciones, a la derecha, **con el primario último**. */
  children: ReactNode
}

/**
 * El pie de acciones de los 19 formularios del sistema (FR-030).
 *
 * Leyenda de obligatorios a la izquierda, acciones a la derecha, primario último. Siempre en el
 * mismo lugar y siempre con el mismo orden: es lo que permite guardar sin volver a buscar el botón
 * en cada pantalla.
 *
 * La jerarquía de cada acción **no se decide acá**: la declara cada `Boton` con su `variante`, que es
 * obligatoria. Esta primitiva da el lugar y el orden; no estila a sus hijos por descendiente, que es
 * exactamente lo que la convención [007] prohíbe.
 */
export function BarraDeAcciones({ anclaje, leyenda, children }: Props) {
  return (
    <div
      className={cn(
        'flex flex-wrap items-center justify-between gap-4',
        anclaje === 'viewport'
          ? // Fija al pie de la ventana: la acción principal es alcanzable desde cualquier punto
            // del desplazamiento (FR-030, SC-003).
            'sticky bottom-0 z-20 rounded-b-card border-t border-line bg-white/95 px-[30px] py-4 backdrop-blur-sm'
          : 'mt-2 border-t border-line pt-[18px]',
      )}
    >
      {leyenda !== undefined ? (
        <p className="m-0 text-[11.5px] text-faint">{leyenda}</p>
      ) : (
        <span />
      )}

      <div className="flex flex-wrap items-center gap-2.5">{children}</div>
    </div>
  )
}
