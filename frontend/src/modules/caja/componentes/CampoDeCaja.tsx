import type { ReactNode } from 'react'
import { cn } from '../../../compartido/ui/cn'

interface Props {
  id: string
  etiqueta: string
  /** El ancho comunica el dato: los importes son cortos; el concepto y la referencia, largos. */
  ancho: string
  error: string | null
  ayuda?: string
  children: ReactNode
}

/** Un campo con la anatomía `.campo` de los formularios: etiqueta, control, ayuda y error debajo. */
export function CampoDeCaja({ id, etiqueta, ancho, error, ayuda, children }: Props) {
  return (
    <div className={cn('campo', error !== null && 'con-error', ancho)}>
      <label htmlFor={id}>{etiqueta}</label>
      {children}
      {ayuda !== undefined && (
        <p id={`ayuda-${id}`} className="m-0 text-[11.5px] text-faint">
          {ayuda}
        </p>
      )}
      {error !== null && (
        <p className="campo__error" id={`error-${id}`}>
          {error}
        </p>
      )}
    </div>
  )
}
