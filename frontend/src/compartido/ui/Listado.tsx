import type { ReactNode } from 'react'
import { cn } from './cn'

/**
 * El contenedor de un listado: filtros, tabla y paginación leídos como una sola pieza.
 *
 * **La tabla sigue siendo una `<table>` de verdad.** Esta primitiva no la envuelve en un componente:
 * le da estilo por selector de descendiente, desde acá. Es deliberado —las 21 tablas del sistema ya
 * tienen marcado semántico correcto, con su `caption`, sus `th scope` y sus `tr`, y tocarlas una por
 * una sería veintiuna oportunidades de romper un test sin ganar nada—.
 *
 * Lo que cambia respecto del Módulo 7:
 *
 * - **Se retira la alternancia de fondo** (FR-037). Con divisores hairline alcanza para seguir una
 *   fila; las dos cosas juntas ensucian y hacen que la tabla pese más que su contenido.
 * - Los **encabezados van en eyebrow** —9,5 px Bold, mayúsculas, tracking ancho— sobre `surface-soft`
 *   y en `encabezado`, que es el gris recalibrado a 4,59:1 sobre ese fondo (FR-036).
 * - **La altura de fila es pareja**: `px-5 py-[13px]`, y el dato secundario que haría crecer una fila
 *   va como metadata de 11,5 px debajo del principal, no en su propia línea de tamaño completo.
 * - Los importes se alinean a la derecha, en negrita y **en una sola línea** (FR-039): alcanza con
 *   ponerle `text-right` al `th` y a los `td` de esa columna.
 *
 * El `scope` de cada `th` se conserva tal cual: es lo que hace que un lector de pantalla anuncie la
 * columna al entrar a cada celda.
 */
interface Props {
  children: ReactNode
  className?: string
}

export function Listado({ children, className }: Props) {
  return (
    <section
      className={cn(
        'overflow-hidden rounded-card border border-line bg-surface shadow-card',

        // La tabla, estilada desde el contenedor que la enmarca.
        '[&_table]:w-full [&_table]:border-collapse [&_table]:text-[13px]',
        '[&_caption]:sr-only',

        // Encabezado en eyebrow, sobre `surface-soft` (FR-036).
        '[&_thead]:bg-surface-soft',
        '[&_th]:border-b [&_th]:border-line [&_th]:px-5 [&_th]:py-2.5',
        '[&_th]:text-left [&_th]:text-[9.5px] [&_th]:font-bold',
        '[&_th]:tracking-[0.14em] [&_th]:text-encabezado [&_th]:uppercase',
        '[&_th]:whitespace-nowrap',

        // Cuerpo: sólo divisores hairline, sin alternancia (FR-037).
        '[&_tbody_tr]:border-b [&_tbody_tr]:border-line',
        '[&_tbody_tr:last-child]:border-b-0',
        '[&_tbody_tr]:transition-colors [&_tbody_tr]:duration-200 [&_tbody_tr]:ease-gt',
        '[&_tbody_tr:hover]:bg-surface-soft',
        '[&_td]:px-5 [&_td]:py-[13px] [&_td]:align-middle [&_td]:text-ink',

        // Pie de tabla —totales—, con el mismo fondo que el encabezado.
        '[&_tfoot]:bg-surface-soft [&_tfoot_td]:border-t [&_tfoot_td]:border-line',
        '[&_tfoot_td]:font-bold',

        className,
      )}
    >
      {children}
    </section>
  )
}

/**
 * El envoltorio de la tabla, que es lo que contiene el desplazamiento horizontal cuando las columnas
 * no entran: **se mueve la tabla, no la pantalla** (FR-049). El desplazamiento queda adentro de la
 * isla, así que el resto de la pantalla no se corre.
 */
export function TablaDesplazable({ children, className }: Props) {
  return <div className={cn('overflow-x-auto', className)}>{children}</div>
}
