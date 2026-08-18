import type { ReactNode } from 'react'
import { cn } from './cn'

/**
 * Un campo de formulario: etiqueta, control y, cuando hace falta, ayuda y error.
 *
 * **Envuelve controles nativos; no los reemplaza.** El control va como `children` y sigue siendo un
 * `<input>`, un `<select>` o un `<textarea>` de verdad. No es una limitación: 17 pantallas usan
 * selectores y 10 archivos de test hacen 28 llamadas a `selectOptions` sobre ellos, y además un
 * control nativo ya funciona con teclado, con lector de pantalla y al 200 % de zoom (research §3).
 *
 * La asociación `id` ↔ etiqueta ↔ error se conserva tal cual estaba: es lo que consultan los 138
 * `getByLabelText` de la suite.
 */

const ANCHOS = {
  corto: 'max-w-campo-corto', // CUIT, patente, importe, fecha
  medio: 'max-w-campo-medio', // nombre, teléfono, email
  largo: 'max-w-campo-largo', // razón social, domicilio, origen y destino
  completo: 'w-full', // detalle, observaciones
} as const

export type AnchoDeCampo = keyof typeof ANCHOS

interface Props {
  id: string
  etiqueta: string
  /** Marca visible **y** en el nombre accesible: un asterisco solo no se lee (FR-026). */
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
    <div className={cn('flex flex-col gap-1', ANCHOS[ancho], className)}>
      <label htmlFor={id} className="text-sm font-medium text-texto">
        {etiqueta}
        {obligatorio && (
          <span className="ml-1 text-error" title="Obligatorio">
            *
          </span>
        )}
      </label>

      {ayuda !== undefined && (
        <p id={`${id}-ayuda`} className="text-xs text-texto-suave">
          {ayuda}
        </p>
      )}

      {children}

      {error !== null && error !== '' && (
        <p id={`${id}-error`} className="flex items-start gap-1 text-sm font-medium text-error">
          {/* El borde rojo del control no alcanza: el error se dice con palabras (FR-027). */}
          <span aria-hidden="true">▲</span>
          <span>{error}</span>
        </p>
      )}
    </div>
  )
}
