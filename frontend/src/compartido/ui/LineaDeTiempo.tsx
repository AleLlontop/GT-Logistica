import type { ReactNode } from 'react'
import { cn } from './cn'

/**
 * El historial de quién hizo qué y cuándo, como **línea de tiempo** (FR-054).
 *
 * Reemplaza a `Historial` y, sobre todo, a las **dos tablas de historial de cuatro columnas** de
 * `FichaViaje` y `FichaFactura`. Ahí el par `Estado anterior` + `Estado nuevo` ocupaba dos columnas
 * para nombrar una sola cosa —la transición—, y obligaba a leer en horizontal algo que se lee en
 * vertical: la transición completa va en **una sola línea**, `Pendiente → Facturado`.
 *
 * No se pierde nada al dejar de ser tabla: el historial no tiene columnas que alguien compare en
 * vertical, tiene entradas que se leen de arriba abajo. Y el **paso actual va marcado**, que es lo
 * que una tabla no podía decir.
 */
export function LineaDeTiempo({ children, className }: { children: ReactNode; className?: string }) {
  return <ol className={cn('m-0 flex list-none flex-col gap-0 p-0', className)}>{children}</ol>
}

interface EntradaProps {
  /** Cuándo, ya formateado con `compartido/fechas`. */
  cuando: ReactNode
  /** La transición completa, no dos columnas: `Pendiente → Facturado`. */
  que: ReactNode
  /** Quién lo hizo. */
  quien?: ReactNode
  /** El motivo, cuando lo hay. Se lee como párrafo aunque tenga 500 caracteres. */
  motivo?: ReactNode
  /** El paso actual va marcado. */
  actual?: boolean
}

export function EntradaDeLineaDeTiempo({ cuando, que, quien, motivo, actual = false }: EntradaProps) {
  return (
    <li className="relative border-l border-line py-3 pl-6 last:border-l-transparent">
      {/*
        El punto de la línea. El actual es lleno y en `ink`; los demás, huecos. Decorativo: lo que
        informa es el texto, y el paso actual además se distingue por el peso de su tipografía.
      */}
      <span
        aria-hidden="true"
        /* -4,5px = medio punto (4px) menos medio borde (0,5px): lo centra sobre la línea. */
        className={cn(
          'absolute top-[18px] -left-[4.5px] size-2 rounded-pastilla border',
          actual ? 'border-ink bg-ink' : 'border-line-strong bg-white',
        )}
      />

      <div className="flex flex-wrap items-baseline gap-x-2 gap-y-1">
        <span
          className={cn(
            'text-[13px] tracking-tight text-ink',
            actual ? 'font-bold' : 'font-medium',
          )}
        >
          {que}
        </span>
        <span className="text-[11.5px] text-faint">{cuando}</span>
        {quien !== undefined && (
          <span className="text-[11.5px] text-faint">
            <span aria-hidden="true" className="text-dim">
              ·{' '}
            </span>
            {quien}
          </span>
        )}
      </div>

      {motivo !== undefined && (
        <p className="m-0 mt-1.5 max-w-prose text-[12.5px] leading-5 text-muted">{motivo}</p>
      )}
    </li>
  )
}
