import type { ReactNode } from 'react'
import { cn } from './cn'

/**
 * Las piezas de una ficha.
 *
 * `FichaEncabezado` es el único movimiento de estructura de toda la feature: las acciones de una
 * ficha estaban al pie, después de recorrer toda la pantalla, y suben al encabezado junto a la
 * identidad del registro y su estado (FR-031). El resto es estilo sobre marcado que ya existía: las
 * fichas ya usaban `<section aria-labelledby>` con su `<h2>` y `<dl>`.
 */

interface EncabezadoProps {
  /** Qué registro es: el número de comprobante, la patente, el apellido y nombre. */
  identidad: ReactNode
  /** El indicador de estado, cuando el registro tiene uno. */
  estado?: ReactNode
  /** Los datos que acompañan a la identidad sin ser acciones: cliente, fecha, período. */
  resumen?: ReactNode
  /** Las acciones de escritura. Ausentes en un registro inmutable, y eso se explica con `nota`. */
  acciones?: ReactNode
  /** Por qué no hay acciones, cuando no las hay: "Una factura anulada no se modifica" (FR-033). */
  nota?: ReactNode
}

export function FichaEncabezado({ identidad, estado, resumen, acciones, nota }: EncabezadoProps) {
  return (
    <header className="rounded-medio border border-borde bg-superficie p-5 shadow-tarjeta">
      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <div className="flex flex-wrap items-center gap-3">
            <h1 className="text-2xl font-semibold text-texto">{identidad}</h1>
            {estado}
          </div>

          {resumen !== undefined && (
            <div className="mt-1 text-sm text-texto-suave">{resumen}</div>
          )}
        </div>

        {acciones !== undefined && <div className="flex flex-wrap gap-2">{acciones}</div>}
      </div>

      {nota !== undefined && (
        <p className="mt-4 border-t border-borde pt-3 text-sm text-texto-suave">{nota}</p>
      )}
    </header>
  )
}

interface SeccionProps {
  titulo: string
  /** El `id` del `<h2>`, para el `aria-labelledby` que las fichas ya traen. */
  id: string
  children: ReactNode
  className?: string
}

export function FichaSeccion({ titulo, id, children, className }: SeccionProps) {
  return (
    <section
      aria-labelledby={id}
      className={cn(
        'rounded-medio border border-borde bg-superficie shadow-tarjeta',

        // Las listas de definición de las fichas, estiladas desde acá: dos columnas, rótulo suave
        // y valor con el peso del dato.
        '[&_dl]:grid [&_dl]:gap-x-6 [&_dl]:gap-y-2 [&_dl]:px-5 [&_dl]:py-4',
        '[&_dl]:grid-cols-[minmax(9rem,auto)_1fr]',
        '[&_dt]:text-sm [&_dt]:text-texto-suave',
        '[&_dd]:m-0 [&_dd]:text-sm [&_dd]:font-medium [&_dd]:text-texto',
        className,
      )}
    >
      <h2
        id={id}
        className="border-b border-borde px-5 py-3 text-sm font-semibold tracking-wide text-texto-suave uppercase"
      >
        {titulo}
      </h2>

      {children}
    </section>
  )
}

/** El cuerpo de la ficha: las secciones, una debajo de la otra. */
export function FichaCuerpo({ children, className }: { children: ReactNode; className?: string }) {
  return <div className={cn('flex flex-col gap-4', className)}>{children}</div>
}
