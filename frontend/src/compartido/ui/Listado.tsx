import type { ReactNode } from 'react'
import { cn } from './cn'

/**
 * El contenedor de un listado: filtros, tabla y paginación leídos como una sola pieza (FR-018).
 *
 * **La tabla sigue siendo una `<table>` de verdad.** Esta primitiva no la envuelve en un componente:
 * le da estilo por selector de descendiente, desde acá. Es deliberado —las 20 tablas del sistema ya
 * tienen marcado semántico correcto, con su `caption`, sus `th scope` y sus `tr`, y tocarlas una por
 * una sería veinte oportunidades de romper un test sin ganar nada (research §2)—.
 *
 * Lo que la tabla obtiene: encabezado distinguible del cuerpo (FR-019), filas separadas a simple
 * vista con alternancia de fondo, importes alineados a la derecha con cifras tabulares (FR-020) y
 * desplazamiento contenido cuando no entra, sin arrastrar al resto de la pantalla (FR-044).
 *
 * Para alinear una columna de importes alcanza con ponerle `text-right` a su `th` y a sus `td`.
 */
interface Props {
  children: ReactNode
  className?: string
}

export function Listado({ children, className }: Props) {
  return (
    <section
      className={cn(
        'overflow-hidden rounded-medio border border-borde bg-superficie shadow-tarjeta',

        // La tabla, estilada desde el contenedor que la enmarca.
        '[&_table]:w-full [&_table]:border-collapse [&_table]:text-sm',
        '[&_caption]:sr-only',

        // Encabezado: se distingue del cuerpo por fondo, peso y una línea más marcada (FR-019).
        '[&_thead]:bg-superficie-hundida',
        '[&_th]:border-b [&_th]:border-borde-fuerte [&_th]:px-4 [&_th]:py-2.5',
        '[&_th]:text-left [&_th]:font-semibold [&_th]:text-texto',
        '[&_th]:whitespace-nowrap',

        // Cuerpo: alternancia de fondo y una línea suave. Las dos cosas juntas son lo que permite
        // seguir una fila sin el dedo (FR-019).
        '[&_tbody_tr]:border-b [&_tbody_tr]:border-borde',
        '[&_tbody_tr:last-child]:border-b-0',
        '[&_tbody_tr:nth-child(even)]:bg-superficie-alterna',
        '[&_tbody_tr:hover]:bg-acento-fondo',
        '[&_td]:px-4 [&_td]:py-2.5 [&_td]:align-top',

        // Las acciones de una celda: accesos a un detalle, nunca la acción principal (FR-022).
        '[&_td_a]:text-acento [&_td_a]:underline [&_td_a]:underline-offset-2',
        '[&_td_button]:rounded-chico [&_td_button]:border [&_td_button]:border-borde-fuerte',
        '[&_td_button]:bg-superficie [&_td_button]:px-2 [&_td_button]:py-1 [&_td_button]:text-xs',
        '[&_td_button:hover]:bg-superficie-hundida',
        '[&_td_button:disabled]:text-texto-tenue [&_td_button:disabled]:cursor-not-allowed',

        className,
      )}
    >
      {children}
    </section>
  )
}

/**
 * El envoltorio de la tabla, que es lo que contiene el desplazamiento horizontal cuando las columnas
 * no entran: se mueve la tabla, no la pantalla (FR-044).
 */
export function TablaDesplazable({ children, className }: Props) {
  return <div className={cn('overflow-x-auto', className)}>{children}</div>
}
