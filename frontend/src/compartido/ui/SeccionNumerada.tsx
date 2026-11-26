import type { ReactNode } from 'react'

interface Props {
  numero: number
  titulo: string
  /** Una línea que explica qué se pide, cuando ayuda. */
  explicacion?: string
  /** Para campos breves relacionados, con lectura por filas en escritorios. */
  enColumnas?: boolean
  children: ReactNode
}

/**
 * Un grupo de campos con chip numerado, título y —cuando ayuda— una línea que explica qué se pide
 * (FR-029).
 *
 * El número no es decoración: da orden de lectura y sensación de avance en un formulario largo. Por
 * eso se usa **sólo en los 9 formularios de dos o más grupos**. Los 7 de un solo grupo no la usan: un
 * chip numerado sobre un único grupo no agrupa nada, sólo agrega una etiqueta que no distingue nada
 * de nada.
 *
 * El título va como `<h2>` para que quede en el árbol de encabezados: en un formulario de cuatro
 * secciones, poder saltar de una a otra con el navegador de encabezados es la diferencia entre
 * recorrerlo y perderse adentro.
 */
export function SeccionNumerada({ numero, titulo, explicacion, enColumnas = false, children }: Props) {
  return (
    <section className="border-b border-line px-4 py-5 last-of-type:border-b-0 sm:px-[30px]">
      <header className="mb-4 flex flex-col gap-1">
        <div className="flex items-center gap-2.5">
          <span
            aria-hidden="true"
            className="flex size-6 items-center justify-center rounded-chip border border-line bg-surface-mute text-[11.5px] font-bold text-ink-soft"
          >
            {numero}
          </span>
          <h2 className="m-0 text-[14.5px] font-bold tracking-[-0.02em] text-ink">{titulo}</h2>
        </div>

        {explicacion !== undefined && (
          <p className="m-0 text-[12.5px] text-faint">{explicacion}</p>
        )}
      </header>

      <div className={enColumnas
        ? 'grid items-start gap-4 sm:grid-cols-[repeat(2,minmax(0,24rem))] [&>*]:min-w-0'
        : 'flex flex-col gap-4'}>
        {children}
      </div>
    </section>
  )
}
