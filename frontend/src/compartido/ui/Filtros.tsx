import type { ReactNode } from 'react'
import { cn } from './cn'

/**
 * El bloque de filtros de un listado: **el contenedor de las tres franjas** (FR-032).
 *
 * De arriba abajo: el buscador a todo el ancho, los filtros debajo como desplegables compactos, y el
 * resumen al pie. La primitiva **da el lugar y el estilo; no decide el orden de sus hijos**: los
 * controles llegan como `children` y es cada superficie de filtro la que los ordena, porque cuáles
 * hay y cuál es el buscador lo sabe la pantalla y no el contenedor.
 *
 * Eso significa que *"el buscador primero y a todo el ancho"* (FR-033) y *"el que está filtrando se
 * distingue"* (FR-034) se cumplen **archivo por archivo**, en las ocho superficies de filtro del
 * sistema. Reestilar este contenedor sin tocarlas dejaría las dos reglas sin cumplir en las ocho
 * pantallas que filtran.
 */
interface Props {
  children: ReactNode
  /** El texto que declara qué se está mostrando. Un listado nunca oculta filas en silencio ([003]). */
  declaracion?: ReactNode
  /**
   * La **franja de resumen** (FR-035): cantidad de resultados, el total o las señales que la pantalla
   * tenga —*1 con documentación vencida*— y el criterio de orden. Va **debajo de los filtros y encima
   * de la tabla**, y la arma cada listado porque los tres datos son suyos.
   *
   * Es una prop aparte de `declaracion` y no lo mismo: `declaracion` responde *qué se está mostrando*
   * y `resumen` responde *cuántos, cuánto suman y en qué orden*. Juntarlas en una sola obligaría a
   * cada listado a decidir el orden visual de dos cosas que la spec ya ordenó.
   */
  resumen?: ReactNode
  className?: string
}

export function Filtros({ children, declaracion, resumen, className }: Props) {
  return (
    <div className={cn('border-b border-line', className)}>
      <div className="flex flex-col gap-3.5 px-5 py-4">{children}</div>

      {declaracion !== undefined && (
        <p className="m-0 border-t border-line px-5 py-2.5 text-[12.5px] text-ink-soft">
          {declaracion}
        </p>
      )}

      {resumen !== undefined && (
        <div className="flex flex-wrap items-center gap-x-4 gap-y-1 border-t border-line bg-surface-soft px-5 py-2.5 text-[11.5px] text-muted">
          {resumen}
        </div>
      )}
    </div>
  )
}

/**
 * La franja de arriba: el buscador, **a todo el ancho** (FR-033).
 *
 * Cuando una pantalla no tiene búsqueda por texto —`FiltrosFacturas` y `FiltrosFlota` filtran sólo
 * con desplegables— esta franja simplemente no se dibuja. **No se inventa un buscador** que la
 * pantalla no tiene.
 */
export function FranjaDeBusqueda({ children }: { children: ReactNode }) {
  return <div className="flex flex-wrap items-end gap-3.5 [&>*]:min-w-0 [&>*]:flex-1">{children}</div>
}

/** La franja del medio: los filtros, como desplegables compactos que muestran su valor actual. */
export function FranjaDeFiltros({ children }: { children: ReactNode }) {
  return <div className="flex flex-wrap items-start gap-3.5">{children}</div>
}
