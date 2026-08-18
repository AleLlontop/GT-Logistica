import type { VariantProps } from 'class-variance-authority'
import { cva } from 'class-variance-authority'
import { cn } from './cn'

export const estilosDeBoton = cva(
  'inline-flex items-center justify-center gap-2 rounded-chico font-medium ' +
    'transition-colors disabled:cursor-not-allowed',
  {
    variants: {
      variante: {
        primario:
          'bg-acento text-white hover:bg-acento-oscuro ' +
          'disabled:bg-borde-fuerte disabled:text-white',
        secundario:
          'border border-borde-fuerte bg-superficie text-texto hover:bg-superficie-hundida ' +
          'disabled:border-borde disabled:text-texto-tenue disabled:hover:bg-superficie',
        texto:
          'text-acento underline underline-offset-2 hover:text-acento-oscuro ' +
          'disabled:text-texto-tenue disabled:no-underline',
        destructivo:
          'bg-error text-white hover:brightness-90 ' +
          'disabled:bg-borde-fuerte disabled:text-white',
      },
      tamanio: {
        normal: 'px-4 py-2 text-sm',
        chico: 'px-2.5 py-1 text-sm',
      },
    },
    defaultVariants: { tamanio: 'normal' },
  },
)

/**
 * Las clases del sistema para elementos que **no** son componentes propios: un enlace que tiene
 * que verse como botón, un control nativo, la celda que abre una ficha.
 *
 * Viven acá y no junto a sus componentes porque un archivo que exporta componentes y funciones a
 * la vez rompe el refresco en caliente.
 */

type Variante = NonNullable<VariantProps<typeof estilosDeBoton>['variante']>
type Tamanio = NonNullable<VariantProps<typeof estilosDeBoton>['tamanio']>

/**
 * Las mismas clases, para los pocos casos en que el elemento tiene que ser un enlace de navegación
 * y no un botón —por ejemplo *Nueva factura*, que lleva a otra pantalla—. Un enlace que navega es un
 * enlace, aunque se vea como un botón.
 */
export function clasesDeBoton(variante: Variante, tamanio?: Tamanio, className?: string) {
  return cn(estilosDeBoton({ variante, tamanio }), className)
}

/**
 * Las clases de todo control nativo del sistema: `input`, `select` y `textarea`.
 *
 * Va como función y no como regla global sobre el elemento **a propósito**: una regla global sobre
 * `input` es la misma clase de error que la que pintaba todos los botones de azul. Acá cada control
 * declara que quiere el estilo del sistema.
 */
export function clasesDeControl(conError = false, className?: string) {
  return cn(
    'w-full rounded-chico border bg-superficie px-3 py-2 text-sm text-texto',
    'placeholder:text-texto-tenue disabled:bg-superficie-hundida disabled:text-texto-tenue',
    conError ? 'border-error border-2' : 'border-borde-fuerte',
    className,
  )
}

/**
 * La celda que abre la ficha de una fila.
 *
 * Se ve como acceso a un detalle y **no** como la acción principal de la pantalla (FR-022). Es
 * exactamente el defecto que el módulo corrige: antes cada listado mostraba una columna de botones
 * azules gruesos, porque una regla global pintaba todos los `button`.
 */
export function clasesDeEnlaceDeFila(className?: string) {
  return cn(
    'font-medium text-acento underline underline-offset-2 hover:text-acento-oscuro',
    className,
  )
}

/**
 * La anatomía de **todos** los formularios del sistema, aplicada al `<form>` y resuelta por
 * descendiente.
 *
 * Va así, y no reescribiendo los dieciséis formularios campo por campo, por la misma razón que las
 * tablas: el marcado que tienen ya es correcto —`<div class="campo">` con su `<label htmlFor>`, su
 * control nativo y su `<p class="campo__error" role="alert">`— y tocarlo uno por uno serían
 * dieciséis oportunidades de romper alguno de los 138 `getByLabelText` de la suite sin ganar nada.
 *
 * Lo que resuelve, y que antes no se veía porque ninguna de esas clases existía en ninguna hoja:
 *
 * - **`campo__error` y `con-error`**: el mensaje se ve junto a su campo y el control queda con borde
 *   rojo y más grueso, o sea marcado por algo más que el color (FR-027)
 * - **`acciones`**: agrupadas al pie, siempre en el mismo lugar, y con la que **guarda** —el
 *   `submit`— distinguida de la que cancela (FR-028)
 * - **el control deshabilitado se ve deshabilitado** y no se confunde con uno disponible (FR-029)
 */
export const clasesDeFormulario = cn(
  'flex flex-col gap-4',

  // Cada campo: etiqueta arriba, control debajo.
  '[&_.campo]:flex [&_.campo]:flex-col [&_.campo]:gap-1',
  '[&_.campo>label]:text-sm [&_.campo>label]:font-medium [&_.campo>label]:text-texto',

  // Los controles nativos, estilados sin ser reemplazados.
  '[&_input]:rounded-chico [&_input]:border [&_input]:border-borde-fuerte [&_input]:bg-superficie',
  '[&_input]:px-3 [&_input]:py-2 [&_input]:text-sm [&_input]:text-texto',
  '[&_select]:rounded-chico [&_select]:border [&_select]:border-borde-fuerte [&_select]:bg-superficie',
  '[&_select]:px-3 [&_select]:py-2 [&_select]:text-sm [&_select]:text-texto',
  '[&_textarea]:rounded-chico [&_textarea]:border [&_textarea]:border-borde-fuerte',
  '[&_textarea]:bg-superficie [&_textarea]:px-3 [&_textarea]:py-2 [&_textarea]:text-sm',
  '[&_input[type=checkbox]]:size-4 [&_input[type=checkbox]]:w-auto',

  // Deshabilitado: se ve deshabilitado (FR-029).
  '[&_:disabled]:bg-superficie-hundida [&_:disabled]:text-texto-tenue [&_:disabled]:cursor-not-allowed',

  // El campo con error: borde rojo más grueso, y el mensaje debajo con su palabra (FR-027).
  '[&_.con-error_input]:border-error [&_.con-error_input]:border-2',
  '[&_.con-error_select]:border-error [&_.con-error_select]:border-2',
  '[&_.con-error_textarea]:border-error [&_.con-error_textarea]:border-2',
  '[&_.campo__error]:text-sm [&_.campo__error]:font-medium [&_.campo__error]:text-error',
  '[&_.formulario__error]:rounded-medio [&_.formulario__error]:border-l-4',
  '[&_.formulario__error]:border-error [&_.formulario__error]:bg-error-fondo',
  '[&_.formulario__error]:px-4 [&_.formulario__error]:py-3 [&_.formulario__error]:font-medium',
  '[&_.formulario__error]:text-error',

  // Las acciones, siempre al pie y siempre en el mismo lugar (FR-028).
  '[&_.acciones]:mt-2 [&_.acciones]:flex [&_.acciones]:flex-wrap [&_.acciones]:gap-2',
  '[&_.acciones]:border-t [&_.acciones]:border-borde [&_.acciones]:pt-4',
  '[&_.acciones_button]:rounded-chico [&_.acciones_button]:px-4 [&_.acciones_button]:py-2',
  '[&_.acciones_button]:text-sm [&_.acciones_button]:font-medium',
  // La secundaria por defecto…
  '[&_.acciones_button]:border [&_.acciones_button]:border-borde-fuerte',
  '[&_.acciones_button]:bg-superficie [&_.acciones_button]:text-texto',
  // …y la que guarda, distinguida.
  '[&_.acciones_button[type=submit]]:border-acento [&_.acciones_button[type=submit]]:bg-acento',
  '[&_.acciones_button[type=submit]]:text-white',
  '[&_.acciones_button:disabled]:border-borde [&_.acciones_button:disabled]:bg-borde-fuerte',
  '[&_.acciones_button:disabled]:text-white',
)
