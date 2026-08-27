import type { ReactNode } from 'react'
import { cn } from './cn'
import { IconoVencido } from './iconos'

/**
 * Un campo de formulario: etiqueta, control y, cuando hace falta, ayuda y error.
 *
 * **Envuelve controles nativos; no los reemplaza.** El control va como `children` y sigue siendo un
 * `<input>`, un `<select>` o un `<textarea>` de verdad. No es una limitación: 17 pantallas usan
 * selectores y 10 archivos de test hacen 28 llamadas a `selectOptions` sobre ellos, y además un
 * control nativo ya funciona con teclado, con lector de pantalla y al 200 % de zoom (FR-031,
 * convención [007]).
 *
 * La asociación `id` ↔ etiqueta ↔ error se conserva tal cual estaba: es lo que consultan los 138
 * `getByLabelText` de la suite, y la firma tampoco cambia (contracts §1).
 *
 * Los cuatro estados —reposo, foco, error y deshabilitado— los pone `clasesDeControl` sobre el
 * control que va adentro. Lo que decide esta primitiva es lo que **rodea** al control: el ancho, la
 * marca de obligatorio, la ayuda y el mensaje de error.
 */

const ANCHOS = {
  corto: 'max-w-campo-corto', // DNI, CUIT, patente, fecha, importe, punto de venta, días de aviso
  medio: 'max-w-campo-medio', // nombre, apellido, teléfono, email, marca, modelo, comprobante
  largo: 'max-w-campo-largo', // razón social, domicilio, origen, destino, los `select` de padrón
  completo: 'w-full', // detalle, observaciones
} as const

export type AnchoDeCampo = keyof typeof ANCHOS

interface Props {
  id: string
  etiqueta: string
  /** Marca visible **y** en el nombre accesible: un asterisco solo no se lee (FR-027). */
  obligatorio?: boolean
  error?: string | null
  ayuda?: string
  ancho?: AnchoDeCampo
  children: ReactNode
  className?: string
}

export function Campo({
  id,
  etiqueta,
  obligatorio = false,
  error = null,
  ayuda,
  ancho = 'medio',
  children,
  className,
}: Props) {
  return (
    <div className={cn('flex flex-col gap-[7px]', ANCHOS[ancho], className)}>
      <div className="flex items-center gap-1.5">
        <label
          htmlFor={id}
          className={cn(
            'text-[12.5px] font-semibold tracking-[-0.01em] text-ink-soft',
            /*
             * La marca de obligatorio va como **contenido generado** y no como un `<span>` adentro
             * de la etiqueta (FR-027). No es un detalle de estilo: un `<span>` con `*` cambia el
             * texto del `<label>`, y con él el nombre accesible del control — los 138
             * `getByLabelText` de la suite consultan por el texto exacto que los Módulos 1 a 6
             * fijaron, y `Nombre de usuario` dejaría de encontrarse porque pasaría a ser
             * `Nombre de usuario *`. Quien comunica la obligatoriedad a la tecnología asistiva es el
             * `required` del control, que ya está en el marcado; el asterisco es su marca visible.
             */
            obligatorio && "after:ml-1 after:font-bold after:text-danger after:content-['*']",
          )}
        >
          {etiqueta}
        </label>
      </div>

      {ayuda !== undefined && (
        <p id={`${id}-ayuda`} className="m-0 text-[11.5px] text-faint">
          {ayuda}
        </p>
      )}

      {children}

      {error !== null && error !== '' && (
        <p
          id={`${id}-error`}
          className="m-0 flex items-center gap-1.5 pt-px text-[11.5px] font-medium text-danger-text"
        >
          {/* El borde rojo del control no alcanza: el error se dice con palabras (FR-024). */}
          <IconoVencido aria-hidden="true" className="size-3 shrink-0" />
          <span>{error}</span>
        </p>
      )}
    </div>
  )
}
