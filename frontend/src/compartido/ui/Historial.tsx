import type { ReactNode } from 'react'
import { cn } from './cn'

/**
 * El historial de quién hizo qué y cuándo.
 *
 * Se lee como una **secuencia en el tiempo** y no como una tabla más (FR-034). La diferencia importa
 * porque en una ficha conviven las dos cosas —los viajes incluidos en una factura son una tabla de
 * datos; su historial es una línea de tiempo— y hoy se ven igual.
 *
 * No es una tabla, así que no se pierde nada: el historial no tiene columnas que alguien compare en
 * vertical, tiene entradas que se leen de arriba abajo.
 */

interface Props {
  children: ReactNode
  className?: string
}

export function Historial({ children, className }: Props) {
  return <ol className={cn('m-0 flex list-none flex-col gap-0 p-0', className)}>{children}</ol>
}

interface EntradaProps {
  /** Cuándo, ya formateado con `compartido/fechas`. */
  cuando: ReactNode
  /** Qué pasó: la transición, el cambio de estado. */
  que: ReactNode
  /** Quién lo hizo. */
  quien?: ReactNode
  /** El motivo, cuando lo hay. Se lee como párrafo aunque tenga 500 caracteres. */
  motivo?: ReactNode
}

export function HistorialEntrada({ cuando, que, quien, motivo }: EntradaProps) {
  return (
    <li className="relative border-l-2 border-borde py-3 pl-6 last:border-l-transparent">
      {/* El punto de la línea de tiempo. Decorativo: lo que informa es el texto. */}
      <span
        aria-hidden="true"
        /* -5px = medio punto (4px) menos medio borde (1px): lo centra sobre la línea. */
        className="absolute top-4 -left-[5px] size-2 rounded-full bg-borde-fuerte"
      />

      <div className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
        <span className="text-sm font-medium text-texto">{que}</span>
        <span className="text-xs text-texto-suave">{cuando}</span>
        {quien !== undefined && <span className="text-xs text-texto-suave">· {quien}</span>}
      </div>

      {motivo !== undefined && (
        <p className="mt-1 max-w-prose text-sm text-texto-suave">{motivo}</p>
      )}
    </li>
  )
}
