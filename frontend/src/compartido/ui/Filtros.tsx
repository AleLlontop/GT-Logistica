import type { ReactNode } from 'react'
import { cn } from './cn'

/**
 * El bloque de filtros de un listado, igual en los cinco módulos que lo tienen.
 *
 * Se presenta como parte del listado y no como un formulario suelto encima de la tabla, que es como
 * se veía antes (FR-018, FR-024).
 */
interface Props {
  children: ReactNode
  /** El texto que declara qué se está mostrando. Un listado nunca oculta filas en silencio ([003]). */
  declaracion?: ReactNode
  className?: string
}

export function Filtros({ children, declaracion, className }: Props) {
  return (
    <div className={cn('border-b border-borde bg-superficie-hundida', className)}>
      <div className="flex flex-wrap items-end gap-4 px-4 py-3">{children}</div>

      {declaracion !== undefined && (
        <p className="border-t border-borde px-4 py-2 text-sm text-texto-suave">{declaracion}</p>
      )}
    </div>
  )
}
