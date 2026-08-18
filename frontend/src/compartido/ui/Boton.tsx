import type { VariantProps } from 'class-variance-authority'
import type { ButtonHTMLAttributes } from 'react'
import { cn } from './cn'
import { estilosDeBoton } from './clases'

/**
 * El botón del sistema.
 *
 * **`variante` es obligatoria y no tiene valor por defecto.** Ése es el punto de esta primitiva y no
 * un detalle de tipos: el defecto que el Módulo 7 corrige nació de una regla global que pintaba
 * *todos* los `button` como acción principal, y por eso la celda que abre la ficha de una fila se
 * veía como el botón más importante de la pantalla. Sin variante, esto no compila.
 *
 * Sigue siendo un `<button>` nativo: los 109 `getByRole` de la suite lo encuentran igual.
 */

type Variante = NonNullable<VariantProps<typeof estilosDeBoton>['variante']>
type Tamanio = NonNullable<VariantProps<typeof estilosDeBoton>['tamanio']>

interface Props extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'className'> {
  variante: Variante
  tamanio?: Tamanio
  className?: string
}

export function Boton({ variante, tamanio, className, type = 'button', ...resto }: Props) {
  return <button type={type} className={cn(estilosDeBoton({ variante, tamanio }), className)} {...resto} />
}
