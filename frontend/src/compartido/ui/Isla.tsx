import type { ReactNode } from 'react'
import { cn } from './cn'

interface Props {
  children: ReactNode
  className?: string
}

/**
 * La superficie de contenido del sistema: toda tarjeta, todo listado, toda sección (FR-004).
 *
 * Blanco al 94 % —el lienzo se ve apenas a través—, hairline de `line` y sombra difusa con spread
 * negativo. **Nunca un borde gris sólido de 1 px ni una sombra dura** (FR-005, FR-015): son las dos
 * cosas que hacen que una interfaz se vea recortada en vez de apoyada.
 *
 * El radio es el de tarjeta. Si una isla anida a otra, los radios tienen que ser concéntricos
 * —el interior es el exterior menos el relleno—, y eso lo resuelve quien anida pasando `className`.
 */
export function Isla({ children, className }: Props) {
  return (
    <div className={cn('rounded-card border border-line bg-surface shadow-card', className)}>
      {children}
    </div>
  )
}
