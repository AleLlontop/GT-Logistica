import { useEffect, useId, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { Link } from 'react-router-dom'
import { cn } from './cn'

export interface ItemDeMenu {
  /** El texto del ítem. Verbo en infinitivo + objeto cuando hay ambigüedad. */
  etiqueta: string
  /** Un ítem que navega es un enlace; uno que ejecuta es un botón (FR-023). Se pasa uno u otro. */
  a?: string
  onSeleccionar?: () => void
  /** Los destructivos van al final y en `danger` (FR-020). */
  destructivo?: boolean
  disabled?: boolean
}

interface Props {
  /** Nombre accesible del disparador: `Acciones de Gómez, Ramona`. Nunca sólo *Acciones*. */
  etiqueta: string
  items: ItemDeMenu[]
}

/**
 * El menú `···` que reemplaza a las columnas `Acciones` de las tablas (FR-044, FR-045).
 *
 * **Se escribe, no se instala.** Radix Dropdown traería foco y flechas gratis, pero convierte cada
 * ítem en un `div[role="menuitem"]`: las aserciones de la suite consultan `getByRole('button', …)` y
 * `getByRole('link', …)`, y FR-069 sólo autoriza a agregarles un paso de interacción, no a
 * reescribirlas. Acá los ítems son `<button>` y `<a>` **de verdad**, así que abrir el menú es lo
 * único que esas veinte líneas necesitan agregar.
 *
 * Las diez reglas de comportamiento, que es lo que de verdad hay que cumplir:
 *
 *  1. El disparador está **siempre en el DOM y siempre es enfocable**. No aparece al pasar el mouse:
 *     uno que aparece con el mouse queda inalcanzable con teclado y en pantalla táctil.
 *  2. `items` vacío → **no se dibuja nada**. Ocultarlo es cortesía; la restricción es el `403`.
 *  3. Abre con clic, `Enter` o `Espacio`. Al abrir, el foco pasa al **primer ítem**.
 *  4. `ArrowDown` / `ArrowUp` recorren los ítems y ciclan.
 *  5. `Escape` cierra y **devuelve el foco al disparador**.
 *  6. El foco que sale del menú lo cierra.
 *  7. **No retiene el foco ni bloquea la pantalla de atrás**: no es un diálogo.
 *  8. Un ítem que dispara una confirmación le entrega el foco al `Dialogo` que ya existe.
 *  9. Los ítems son `<button>` y `<a>` de verdad, no `div[role="menuitem"]`.
 * 10. El clic sobre el disparador **no dispara la navegación de la fila**.
 */
export function MenuDeFila({ etiqueta, items }: Props) {
  const [abierto, setAbierto] = useState(false)
  const [coords, setCoords] = useState<{ top: number; right: number } | null>(null)
  
  const contenedor = useRef<HTMLDivElement>(null)
  const disparador = useRef<HTMLButtonElement>(null)
  const lista = useRef<HTMLDivElement>(null)
  const idLista = useId()

  // Regla 3: al abrir, el foco pasa al primer ítem.
  useEffect(() => {
    if (abierto) {
      enfocables(lista.current)[0]?.focus()
    }
  }, [abierto])

  // Cerrar si la pantalla se scrollea o redimensiona, para que el menú no quede flotando
  // desfasado de su disparador.
  useEffect(() => {
    if (!abierto) return
    const alMover = () => setAbierto(false)
    window.addEventListener('scroll', alMover, { capture: true, passive: true })
    window.addEventListener('resize', alMover, { passive: true })
    return () => {
      window.removeEventListener('scroll', alMover, { capture: true })
      window.removeEventListener('resize', alMover)
    }
  }, [abierto])

  // Regla 2: sin ítems no se dibuja nada. Va después de los hooks para no llamarlos
  // condicionalmente.
  if (items.length === 0) {
    return null
  }

  function cerrarYDevolverElFoco() {
    setAbierto(false)
    disparador.current?.focus()
  }

  function alternar() {
    if (abierto) {
      setAbierto(false)
    } else {
      if (disparador.current) {
        const rect = disparador.current.getBoundingClientRect()
        setCoords({
          top: rect.bottom + window.scrollY,
          right: document.documentElement.clientWidth - rect.right - window.scrollX,
        })
      }
      setAbierto(true)
    }
  }

  return (
    <div
      ref={contenedor}
      className="relative inline-block text-left"
      // Regla 10: el clic no llega al `<tr>` que navega.
      onClick={(evento) => evento.stopPropagation()}
      // Regla 6: el foco que sale del menú lo cierra. `relatedTarget` es adónde va el foco; si
      // sigue adentro del contenedor o de la lista portaled, no salió.
      onBlur={(evento) => {
        const fueAfuera = 
          !contenedor.current?.contains(evento.relatedTarget as Node | null) &&
          !lista.current?.contains(evento.relatedTarget as Node | null)
          
        if (fueAfuera) {
          setAbierto(false)
        }
      }}
      onKeyDown={(evento) => {
        if (!abierto) return

        if (evento.key === 'Escape') {
          evento.preventDefault()
          cerrarYDevolverElFoco() // Regla 5
          return
        }

        // Regla 4: las flechas recorren y ciclan.
        if (evento.key === 'ArrowDown' || evento.key === 'ArrowUp') {
          evento.preventDefault()
          const opciones = enfocables(lista.current)
          if (opciones.length === 0) return

          const actual = opciones.indexOf(document.activeElement as HTMLElement)
          const paso = evento.key === 'ArrowDown' ? 1 : -1
          const siguiente = (actual + paso + opciones.length) % opciones.length
          opciones[siguiente]?.focus()
        }
      }}
    >
      <button
        ref={disparador}
        type="button"
        aria-label={etiqueta}
        aria-haspopup="menu"
        aria-expanded={abierto}
        aria-controls={abierto ? idLista : undefined}
        onClick={alternar}
        className={cn(
          'flex size-7 items-center justify-center rounded-pastilla text-[15px] leading-none',
          'text-muted transition-colors duration-200 ease-gt hover:bg-surface-mute hover:text-ink',
          abierto && 'bg-surface-mute text-ink',
        )}
      >
        <span aria-hidden="true">···</span>
      </button>

      {abierto && coords !== null && createPortal(
        <div
          ref={lista}
          id={idLista}
          style={{ top: coords.top, right: coords.right }}
          /*
           * Regla 7: no es un diálogo. No hay overlay, no hay `aria-modal` y el foco no queda
           * retenido — el fondo se sigue pudiendo usar, y salir del menú lo cierra.
           */
          className="absolute z-[60] mt-1 flex min-w-[11rem] flex-col rounded-card border border-line bg-white/95 py-1.5 shadow-island"
        >
          {items.map((item) => {
            const clases = cn(
              'flex w-full items-center px-3.5 py-2 text-left text-[12.5px] font-medium',
              'no-underline transition-colors duration-200 ease-gt',
              item.destructivo
                ? 'text-danger-text hover:bg-danger-bg'
                : 'text-ink-soft hover:bg-surface-mute hover:text-ink',
              item.disabled === true && 'cursor-not-allowed opacity-50',
            )

            // Regla 9: lo que navega es un `<a>`; lo que ejecuta es un `<button>`.
            return item.a !== undefined ? (
              <Link
                key={item.etiqueta}
                to={item.a}
                className={clases}
                onClick={() => setAbierto(false)}
              >
                {item.etiqueta}
              </Link>
            ) : (
              <button
                key={item.etiqueta}
                type="button"
                disabled={item.disabled}
                className={clases}
                onClick={() => {
                  setAbierto(false)
                  // Regla 8: si el ítem abre una confirmación, el `Dialogo` se lleva el foco solo.
                  item.onSeleccionar?.()
                }}
              >
                {item.etiqueta}
              </button>
            )
          })}
        </div>,
        document.body
      )}
    </div>
  )
}

/** Los ítems que pueden recibir el foco, en el orden en que están dibujados. */
function enfocables(raiz: HTMLElement | null): HTMLElement[] {
  if (raiz === null) return []
  return Array.from(raiz.querySelectorAll<HTMLElement>('a[href], button:not([disabled])'))
}
