import type { ReactNode } from 'react'
import { cn } from './cn'

/**
 * Las piezas de una ficha de solo lectura.
 *
 * El Módulo 7 había subido las acciones al encabezado; lo que esta feature agrega es la **segunda
 * columna** (FR-050) y la **línea de identidad** (FR-051): título, identificador y pastilla de estado
 * en el mismo renglón, con el contexto —cliente, ruta, fecha— debajo. El estado **sale de la lista de
 * datos y sube al encabezado** (FR-052): enterrado como una fila más de un `<dl>` obligaba a
 * recorrerlo entero para saber si el registro se podía operar.
 */

interface EncabezadoProps {
  /** Qué registro es: el número de comprobante, la patente, el apellido y nombre. */
  identidad: ReactNode
  /** El token del identificador, **en la misma línea** que el título y la pastilla (FR-051). */
  token?: ReactNode
  /** El indicador de estado, cuando el registro tiene uno. */
  estado?: ReactNode
  /** El contexto del registro, debajo de la línea de identidad (FR-051). */
  resumen?: ReactNode
  /** Las acciones de escritura. Ausentes en un registro inmutable, y eso lo explica el `Callout`. */
  acciones?: ReactNode
  /** Por qué no hay acciones, cuando no las hay. */
  nota?: ReactNode
}

export function FichaEncabezado({
  identidad,
  token,
  estado,
  resumen,
  acciones,
  nota,
}: EncabezadoProps) {
  return (
    <header className="mb-[18px] flex flex-wrap items-start justify-between gap-4">
      <div className="min-w-0">
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="m-0 text-[34px] leading-[1.15] font-extrabold tracking-[-0.04em] text-ink">
            {identidad}
          </h1>
          {token}
          {estado}
        </div>

        {resumen !== undefined && <div className="mt-1.5 text-[13px] text-ink-soft">{resumen}</div>}

        {nota !== undefined && <div className="mt-2 text-[12.5px] text-ink-soft">{nota}</div>}
      </div>

      {acciones !== undefined && (
        <div className="flex flex-wrap items-center gap-2.5">{acciones}</div>
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

/** `FichaSeccion` no cambia de firma ni de estructura: sólo se reviste con los tokens nuevos. */
export function FichaSeccion({ titulo, id, children, className }: SeccionProps) {
  return (
    <section
      aria-labelledby={id}
      className={cn(
        'overflow-hidden rounded-card border border-line bg-surface shadow-card',

        // Las listas de definición de las fichas, estiladas desde acá: dos columnas, rótulo suave
        // y valor con el peso del dato.
        '[&_dl]:grid [&_dl]:gap-x-6 [&_dl]:gap-y-2.5 [&_dl]:px-[22px] [&_dl]:py-5',
        '[&_dl]:grid-cols-[minmax(9rem,auto)_1fr]',
        '[&_dt]:text-[12.5px] [&_dt]:text-faint',
        '[&_dd]:m-0 [&_dd]:text-[13px] [&_dd]:font-medium [&_dd]:text-ink',
        className,
      )}
    >
      <h2
        id={id}
        className="m-0 border-b border-line px-[22px] py-4 text-[14px] font-bold tracking-[-0.02em] text-ink"
      >
        {titulo}
      </h2>

      {children}
    </section>
  )
}

interface CuerpoProps {
  /** La columna principal, flexible. */
  children: ReactNode
  /**
   * El aside fijo de 330 px. **Obligatorio**: FR-050 dice que nunca queda vacío, y `AsideDeFicha`
   * exige a su vez un `destacado`.
   *
   * Es obligatorio a propósito, por la misma razón que `variante` en `Boton` y `forma` en `Estado`:
   * un opcional dejaría que una de las cinco fichas se olvidara de su dato de más valor sin que nada
   * falle. **Una ficha que no sabe cuál es su dato de más valor no compila.**
   */
  aside: ReactNode
  className?: string
}

export function FichaCuerpo({ children, aside, className }: CuerpoProps) {
  return (
    <div className={cn('flex items-start gap-[18px]', className)}>
      <div className="flex min-w-0 flex-1 flex-col gap-[18px]">{children}</div>
      {aside}
    </div>
  )
}
