import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { cn } from './cn'

interface Props {
  /** Cuando lleva `a` es un enlace; sin `a` es una etiqueta. */
  a?: string
  numero: ReactNode
}

/**
 * El recuadro de un identificador: el `#` atenuado y el número en mono (FR-041).
 *
 * Todo identificador clickeable **se ve clickeable**. Un número de viaje o de comprobante que se
 * dibuja como texto muerto obliga a buscar dónde hacer clic en cada fila, y es lo que las columnas
 * *Ver ficha* venían a tapar.
 *
 * Sin `a` es una etiqueta y no un enlace: la misma forma sirve para mostrar el identificador en el
 * encabezado de una ficha, donde ya se está adentro del registro y no hay adónde ir.
 *
 * El `#` va en `dim`, que acá es **relleno decorativo y no texto informativo**: lo que se lee es el
 * número (FR-007a).
 */
export function TokenDeIdentificador({ a, numero }: Props) {
  const clases = cn(
    'inline-flex w-fit items-center gap-px rounded-token border border-line-strong',
    'bg-surface-mute px-2 py-1 font-mono no-underline',
    'transition-colors duration-200 ease-gt',
    a !== undefined && 'hover:border-brand/40 hover:bg-brand-soft',
  )

  const contenido = (
    <>
      <span aria-hidden="true" className="text-[11.5px] text-dim">
        #
      </span>
      <span className="text-[13px] font-semibold text-ink">{numero}</span>
    </>
  )

  if (a === undefined) {
    return <span className={clases}>{contenido}</span>
  }

  return (
    <Link to={a} className={clases}>
      {contenido}
    </Link>
  )
}
