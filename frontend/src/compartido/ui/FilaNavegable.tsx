import type { ReactNode } from 'react'
import { useNavigate } from 'react-router-dom'
import { cn } from './cn'

interface Props {
  /** El mismo destino que el enlace de la fila. */
  a: string
  children: ReactNode
  /** Lo que la fila entera declara de sí misma: `atenuada` cuando el registro está anulado. */
  className?: string
}

/**
 * La fila que se abre entera con el mouse (FR-042, FR-043).
 *
 * Un `<tr>` con `onClick` que navega y un chevron `›` que aparece al pasar el mouse, para que se vea
 * que la fila lleva a algún lado antes de hacer clic.
 *
 * **No recibe `tabIndex`.** El destino accesible por teclado es el `<a>` de la celda —el
 * `EnlaceDeFila` o el `TokenDeIdentificador`—, y el `onClick` del `<tr>` es comodidad de mouse.
 * Agregarlo al recorrido del tabulador duplicaría cada fila: en un listado de veinte filas serían
 * cuarenta paradas para llegar a las mismas veinte fichas.
 *
 * El chevron va como columna propia y `aria-hidden`: es una señal visual del destino, y ese destino
 * ya está nombrado por el enlace de la celda.
 */
export function FilaNavegable({ a, children, className }: Props) {
  const navegar = useNavigate()

  return (
    <tr className={cn('group cursor-pointer', className)} onClick={() => navegar(a)}>
      {children}

      <td aria-hidden="true" className="w-8 pr-4 text-right">
        <span className="text-[15px] leading-none text-dim opacity-0 transition-opacity duration-200 ease-gt group-hover:opacity-100">
          ›
        </span>
      </td>
    </tr>
  )
}

/**
 * El encabezado de la columna del chevron. Va sin texto y `aria-hidden`, para que el `<thead>` tenga
 * la misma cantidad de columnas que el `<tbody>` sin agregar un rótulo que no nombra nada.
 */
export function EncabezadoDeChevron() {
  return <th aria-hidden="true" scope="col" className="w-8" />
}
