import { useEffect, useRef, type KeyboardEvent } from 'react'

interface Props {
  titulo: string
  mensaje: string
  onConfirmar: () => void
  onCancelar: () => void
}

/**
 * Confirmación explícita previa a una operación que no conviene hacer por accidente: las bajas
 * (FR-017) y el restablecimiento de contraseña. Cancelar no modifica nada.
 *
 * Accesibilidad (contracts/README.md): recibe el foco al abrirse, se cierra con `Escape` —que
 * equivale a cancelar— y devuelve el foco al elemento desde el que se abrió.
 */
export function DialogoConfirmacion({ titulo, mensaje, onConfirmar, onCancelar }: Props) {
  const dialogo = useRef<HTMLDivElement>(null)
  const origen = useRef<Element | null>(null)

  useEffect(() => {
    origen.current = document.activeElement
    dialogo.current?.focus()

    return () => {
      // Devolver el foco a la fila de origen evita que quien navega con teclado quede al principio
      // de la página después de cerrar.
      if (origen.current instanceof HTMLElement) {
        origen.current.focus()
      }
    }
  }, [])

  function alPresionarTecla(evento: KeyboardEvent<HTMLDivElement>) {
    if (evento.key === 'Escape') {
      evento.stopPropagation()
      onCancelar()
    }
  }

  return (
    <div
      ref={dialogo}
      role="dialog"
      aria-modal="true"
      aria-labelledby="titulo-confirmacion"
      tabIndex={-1}
      onKeyDown={alPresionarTecla}
    >
      <h2 id="titulo-confirmacion">{titulo}</h2>
      <p>{mensaje}</p>

      <button type="button" onClick={onConfirmar}>
        Confirmar
      </button>
      <button type="button" onClick={onCancelar}>
        Cancelar
      </button>
    </div>
  )
}
