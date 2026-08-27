import type { VariantProps } from 'class-variance-authority'
import type { ButtonHTMLAttributes, ReactNode } from 'react'
import { cn } from './cn'
import { clasesDeCirculoDeIcono, clasesDeCirculoDeVolver, estilosDeBoton } from './clases'
import { IconoVolver } from './iconos'

/**
 * El botón del sistema.
 *
 * **`variante` es obligatoria y no tiene valor por defecto.** Ése es el punto de esta primitiva y no
 * un detalle de tipos: el defecto que el Módulo 7 corrigió nació de una regla global que pintaba
 * *todos* los `button` como acción principal, y por eso la celda que abría la ficha de una fila se
 * veía como el botón más importante de la pantalla. Sin variante, esto no compila — y cambiar la
 * apariencia de los cuatro niveles no debilita esa garantía (FR-021).
 *
 * Sigue siendo un `<button>` nativo: los 109 `getByRole` de la suite lo encuentran igual.
 *
 * **`icono` es una prop y no un selector sobre los hijos** (FR-018). El círculo interno es un
 * `<span>` que envuelve al ícono, y una cadena de clases no puede crear un elemento: la alternativa
 * era `[&>svg:last-child]`, que es exactamente la regla sobre elementos interactivos a secas que la
 * convención [007] prohíbe y que esta misma feature retira de `EncabezadoDePantalla`. Con la prop, un
 * botón que mete el ícono adentro de `children` se ve mal **y se ve en la revisión**; con el selector
 * se vería mal en silencio.
 *
 * Es **opcional y aditiva**: ninguna llamada existente deja de compilar. Sólo la rinden `primario` y
 * `destructivo`; en `secundario` y `texto` se ignora, y `terciario` dibuja su propia flecha.
 */

type Variante = NonNullable<VariantProps<typeof estilosDeBoton>['variante']>
type Tamanio = NonNullable<VariantProps<typeof estilosDeBoton>['tamanio']>

interface Props extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'className'> {
  variante: Variante
  tamanio?: Tamanio
  /** El ícono de la acción, que la primitiva anida en un círculo pegado al borde derecho. */
  icono?: ReactNode
  className?: string
}

/** Los dos niveles que rinden el círculo interno. En el resto, `icono` no se dibuja. */
const CON_CIRCULO: Variante[] = ['primario', 'destructivo']

export function Boton({
  variante,
  tamanio,
  icono,
  className,
  type = 'button',
  children,
  ...resto
}: Props) {
  const llevaCirculo = icono !== undefined && CON_CIRCULO.includes(variante)

  return (
    <button
      type={type}
      className={cn(
        estilosDeBoton({ variante, tamanio }),
        // Con ícono, el relleno derecho se achica para que el círculo quede pegado al borde.
        llevaCirculo && (tamanio === 'chico' ? 'py-1 pr-1' : 'py-1.5 pr-1.5'),
        className,
      )}
      {...resto}
    >
      {variante === 'terciario' && (
        <span aria-hidden="true" className={clasesDeCirculoDeVolver}>
          <IconoVolver className="size-3 text-muted" />
        </span>
      )}

      {children}

      {llevaCirculo && (
        <span aria-hidden="true" className={clasesDeCirculoDeIcono}>
          {icono}
        </span>
      )}
    </button>
  )
}
