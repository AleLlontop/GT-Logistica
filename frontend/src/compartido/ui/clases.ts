import type { VariantProps } from 'class-variance-authority'
import { cva } from 'class-variance-authority'
import { cn } from './cn'

/**
 * Los cuatro niveles de acción del sistema, con la forma de gt-ui (data-model §2).
 *
 * Cambia la **apariencia** de cada nivel; no cambia la garantía: `variante` sigue siendo obligatoria
 * y sin valor por defecto en `Boton`, y un botón que no declara su nivel no compila (FR-021).
 *
 * - `primario` — pastilla `ink` rellena, con el ícono anidado en su propio círculo interno. Una sola
 *   por pantalla.
 * - `secundario` — pastilla `surface-mute` **sin borde**, del mismo tamaño. Cancelar, y lo que
 *   acompaña al primario en un encabezado.
 * - `terciario` — pastilla blanca con hairline y la flecha en un círculo. *Volver*.
 * - `destructivo` — idéntico al primario, en `bg-danger-text`. **Único uso del rojo como relleno**
 *   (FR-020). El relleno es el rojo **oscuro** de la paleta y no el de borde: con la etiqueta en
 *   blanco encima, `danger` mide 3,91:1 y `danger-text` mide 5,33:1, y el piso es 4,5:1 (SC-009).
 *   No cambia ningún valor: elige, entre los dos rojos que el sistema ya tiene, el que la medición
 *   admite para este par.
 *
 * `texto` **no es un quinto nivel**: es un enlace, y se conserva para los enlaces contextuales de una
 * celda o de una ficha.
 */
export const estilosDeBoton = cva(
  'group inline-flex items-center justify-center rounded-pastilla tracking-tight ' +
    // Se enumera **qué** se anima en vez de dejar `transition-all`: lo único que cambia en un
    // botón es su relleno, su sombra y su escala al apretarlo (FR-006).
    'transition-[background-color,box-shadow,transform] duration-200 ease-gt ' +
    'disabled:cursor-not-allowed',
  {
    variants: {
      variante: {
        primario:
          'gap-3 bg-ink font-bold text-white shadow-btn hover:bg-ink/90 active:scale-[0.98] ' +
          'disabled:bg-dim disabled:text-white disabled:shadow-none disabled:hover:bg-dim',
        secundario:
          'gap-2 bg-surface-mute font-semibold text-ink-soft hover:bg-[#E9EAEF] ' +
          'active:scale-[0.98] disabled:opacity-50 disabled:hover:bg-surface-mute',
        terciario:
          'gap-2.5 border border-line bg-white/90 font-semibold text-ink-soft shadow-pill ' +
          'hover:bg-white active:scale-[0.98] disabled:opacity-50',
        texto:
          'gap-1.5 font-medium text-brand underline underline-offset-2 hover:text-brand-deep ' +
          'disabled:text-faint disabled:no-underline',
        destructivo:
          'gap-3 bg-danger-text font-bold text-white shadow-btn hover:brightness-95 ' +
          'active:scale-[0.98] disabled:bg-dim disabled:text-white disabled:shadow-none',
      },
      tamanio: {
        normal: 'px-5 py-3 text-[13px]',
        chico: 'px-4 py-2 text-[12.5px]',
      },
    },
    compoundVariants: [
      // Un enlace no lleva el relleno de una pastilla: se vería como un botón sin fondo.
      { variante: 'texto', tamanio: 'normal', class: 'p-0 text-[13px]' },
      { variante: 'texto', tamanio: 'chico', class: 'p-0 text-[12.5px]' },
      // El *volver* lleva la flecha en un círculo pegado al borde izquierdo.
      { variante: 'terciario', tamanio: 'normal', class: 'py-1.5 pr-[18px] pl-1.5 text-[12.5px]' },
      { variante: 'terciario', tamanio: 'chico', class: 'py-1 pr-3.5 pl-1 text-[12.5px]' },
    ],
    defaultVariants: { tamanio: 'normal' },
  },
)

/**
 * Las clases del sistema para elementos que **no** son componentes propios: un enlace que tiene
 * que verse como botón, un control nativo.
 *
 * Viven acá y no junto a sus componentes porque un archivo que exporta componentes y funciones a
 * la vez rompe el refresco en caliente.
 */

type Variante = NonNullable<VariantProps<typeof estilosDeBoton>['variante']>
type Tamanio = NonNullable<VariantProps<typeof estilosDeBoton>['tamanio']>

/**
 * Las mismas clases, para los pocos casos en que el elemento tiene que ser un enlace de navegación
 * y no un botón —por ejemplo *Nueva factura*, que lleva a otra pantalla—. Un enlace que navega es un
 * enlace, aunque se vea como un botón (FR-023).
 */
export function clasesDeBoton(variante: Variante, tamanio?: Tamanio, className?: string) {
  return cn(estilosDeBoton({ variante, tamanio }), 'no-underline', className)
}

/**
 * El **círculo interno** que envuelve al ícono de un primario o un destructivo (FR-018).
 *
 * Va acá y no suelto en la primitiva porque también lo necesita `clasesDeBoton`: un enlace que se ve
 * como acción principal tiene que dibujar el mismo círculo que el botón.
 */
export const clasesDeCirculoDeIcono = cn(
  'flex size-8 shrink-0 items-center justify-center rounded-pastilla bg-white/[0.14]',
  'transition-transform duration-200 ease-gt group-hover:translate-x-0.5',
)

/** El círculo de la flecha del *volver*: sobre pastilla blanca, así que el relleno es `surface-mute`. */
export const clasesDeCirculoDeVolver = cn(
  'flex size-7 shrink-0 items-center justify-center rounded-pastilla bg-surface-mute',
)

/**
 * Las clases de todo control nativo del sistema: `input`, `select` y `textarea`.
 *
 * Va como función y no como regla global sobre el elemento **a propósito**: una regla global sobre
 * `input` es la misma clase de error que la que pintaba todos los botones de azul. Acá cada control
 * declara que quiere el estilo del sistema (FR-031).
 *
 * Los cuatro estados salen del contrato de campo del plan: reposo `surface-soft` con borde
 * `line-strong`, foco con **anillo visible por sí solo**, error con borde y fondo `danger`, y
 * deshabilitado atenuado y con el cursor bloqueado.
 */
export function clasesDeControl(conError = false, className?: string) {
  return cn(
    'w-full rounded-field border px-[15px] py-[13px] text-[13.5px] text-ink',
    'placeholder:text-faint transition-shadow duration-200 ease-gt',
    'focus:border-brand focus:bg-white focus:shadow-focus focus:outline-none',
    'disabled:cursor-not-allowed disabled:opacity-50',
    conError
      ? 'border-danger bg-danger-bg focus:shadow-none'
      : 'border-line-strong bg-surface-soft',
    className,
  )
}

/**
 * Las clases de un control de filtro (FR-034).
 *
 * Un filtro no se envía: se aplica. Por eso **no tiene estado de error** y sus estados son tres:
 * reposo, foco y *filtrando*. El que está filtrando lleva **borde más fuerte y fondo propio**, para
 * que se distinga de los que están en su valor por defecto sin que haga falta leerlos uno por uno.
 *
 * `activo` lo decide la superficie de filtro, que es la que sabe cuál es el valor por defecto de cada
 * control: para un `select` es la opción vacía, para una casilla es `false`, para un rango de fechas
 * es no tener ninguna de las dos.
 */
export function clasesDeFiltro(activo: boolean, className?: string) {
  return cn(
    'rounded-field border px-3.5 py-2 text-[12.5px] text-ink',
    'placeholder:text-faint transition-shadow duration-200 ease-gt',
    'focus:border-brand focus:bg-white focus:shadow-focus focus:outline-none',
    activo ? 'border-brand/40 bg-brand-soft font-semibold' : 'border-line-strong bg-surface-soft',
    className,
  )
}

/** La etiqueta de un control de filtro. */
export const clasesDeEtiquetaDeFiltro = cn(
  'text-[9.5px] font-bold tracking-[0.14em] text-encabezado uppercase',
)

/**
 * La anatomía de **todos** los formularios del sistema, aplicada al `<form>` y resuelta por
 * descendiente.
 *
 * Va así, y no reescribiendo los diecinueve formularios campo por campo, por la misma razón que las
 * tablas: el marcado que tienen ya es correcto —`<div class="campo">` con su `<label htmlFor>`, su
 * control nativo y su `<p class="campo__error" role="alert">`— y tocarlo uno por uno serían
 * diecinueve oportunidades de romper alguno de los 138 `getByLabelText` de la suite sin ganar nada.
 *
 * **Es una excepción acotada y declarada** a la regla de [007], no una infracción: no selecciona
 * elementos interactivos *a secas* —lo hace bajo `.campo`, `.con-error` y `.acciones`, que son clases
 * que el marcado del formulario declara— y no decide jerarquía de acción por el tipo del botón. Eso
 * último es lo que `BarraDeAcciones` y la variante de `Boton` pasan a resolver.
 */
export const clasesDeFormulario = cn(
  'flex flex-col gap-[18px]',

  // Cada campo: etiqueta arriba, control debajo.
  '[&_.campo]:flex [&_.campo]:flex-col [&_.campo]:gap-[7px]',
  '[&_.campo>label]:text-[12.5px] [&_.campo>label]:font-semibold',
  '[&_.campo>label]:tracking-[-0.01em] [&_.campo>label]:text-ink-soft',

  // La marca de obligatorio, **derivada del `required` que el control ya declara** (FR-027). Va
  // como contenido generado: un `<span>` con `*` adentro del `<label>` cambiaría el nombre
  // accesible del control, y con él los 138 `getByLabelText` que consultan el texto exacto que
  // fijaron los Módulos 1 a 6. Derivarla del atributo además garantiza que no pueda discrepar de
  // él: no hay dos fuentes que mantener de acuerdo.
  "[&_.campo:has(:required)>label]:after:ml-1",
  "[&_.campo:has(:required)>label]:after:font-bold",
  "[&_.campo:has(:required)>label]:after:text-danger",
  "[&_.campo:has(:required)>label]:after:content-['*']",

  // Los controles nativos, estilados sin ser reemplazados (FR-031).
  '[&_input]:rounded-field [&_input]:border [&_input]:border-line-strong',
  '[&_input]:bg-surface-soft [&_input]:px-[15px] [&_input]:py-[13px]',
  '[&_input]:text-[13.5px] [&_input]:text-ink',
  '[&_select]:rounded-field [&_select]:border [&_select]:border-line-strong',
  '[&_select]:bg-surface-soft [&_select]:px-[15px] [&_select]:py-[13px]',
  '[&_select]:text-[13.5px] [&_select]:text-ink',
  '[&_textarea]:rounded-field [&_textarea]:border [&_textarea]:border-line-strong',
  '[&_textarea]:bg-surface-soft [&_textarea]:px-[15px] [&_textarea]:py-[13px]',
  '[&_textarea]:text-[13.5px] [&_textarea]:text-ink',
  '[&_input]:placeholder:text-faint [&_textarea]:placeholder:text-faint',
  '[&_input[type=checkbox]]:size-4 [&_input[type=checkbox]]:w-auto [&_input[type=checkbox]]:p-0',

  // El anillo de foco, visible por sí solo y no sólo como cambio de color de borde (FR-025).
  '[&_input:focus]:border-brand [&_input:focus]:bg-white [&_input:focus]:shadow-focus',
  '[&_select:focus]:border-brand [&_select:focus]:bg-white [&_select:focus]:shadow-focus',
  '[&_textarea:focus]:border-brand [&_textarea:focus]:bg-white [&_textarea:focus]:shadow-focus',

  // Deshabilitado: se ve deshabilitado y no se confunde con uno disponible.
  '[&_:disabled]:cursor-not-allowed [&_:disabled]:opacity-50',

  // El campo con error: borde y fondo propios, y el mensaje debajo con su palabra (FR-024).
  '[&_.con-error_input]:border-danger [&_.con-error_input]:bg-danger-bg',
  '[&_.con-error_select]:border-danger [&_.con-error_select]:bg-danger-bg',
  '[&_.con-error_textarea]:border-danger [&_.con-error_textarea]:bg-danger-bg',
  '[&_.campo__error]:text-[11.5px] [&_.campo__error]:font-medium',
  '[&_.campo__error]:text-danger-text',
  '[&_.formulario__error]:rounded-card [&_.formulario__error]:border',
  '[&_.formulario__error]:border-line [&_.formulario__error]:bg-danger-bg',
  '[&_.formulario__error]:px-[18px] [&_.formulario__error]:py-4',
  '[&_.formulario__error]:text-[13px] [&_.formulario__error]:font-medium',
  '[&_.formulario__error]:text-danger-text',

  // Las acciones, siempre al pie y siempre en el mismo lugar. La jerarquía **ya no se decide acá**:
  // la declara cada botón con su variante, y `BarraDeAcciones` fija el lugar (FR-030).
  '[&_.acciones]:mt-2 [&_.acciones]:flex [&_.acciones]:flex-wrap [&_.acciones]:gap-2.5',
  '[&_.acciones]:border-t [&_.acciones]:border-line [&_.acciones]:pt-[18px]',
)

/**
 * El formulario **agrupado en secciones numeradas**: la isla es el `<form>` y cada sección es una
 * franja adentro, separada por un hairline (FR-029).
 *
 * `gap-0` porque la separación la da el relleno de cada sección, no el hueco del contenedor: dos
 * separaciones sumadas dejarían el doble de aire entre secciones que adentro de ellas.
 */
export const clasesDeFormularioAgrupado = cn(
  clasesDeFormulario,
  'gap-0 overflow-hidden rounded-card border border-line bg-surface shadow-card',
  // Los avisos de nivel formulario son hijos directos del `<form>`, que no tiene relleno propio:
  // sin esto quedarían pegados al borde de la isla.
  '[&>p]:mx-[30px] [&>p]:mt-[26px] [&>p]:mb-0',
)

/**
 * El formulario de **un solo grupo**: la misma isla, sin franjas. Un chip numerado sobre un único
 * grupo no agrupa nada, así que los campos van directo adentro con el relleno de una tarjeta.
 */
export const clasesDeFormularioSimple = cn(
  clasesDeFormulario,
  'rounded-card border border-line bg-surface p-[26px] shadow-card',
)
