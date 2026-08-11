import { useEffect, useRef, useState, type KeyboardEvent } from 'react'

interface Props {
  numero: number
  onConfirmar: (motivo: string) => void
  onCancelar: () => void
}

/**
 * Confirmación de la anulación de un viaje (FR-036, SC-007, US6 esc. 2 y 3).
 *
 * No reutiliza `DialogoConfirmacion` porque éste pide **un dato**: el motivo, que es obligatorio y
 * queda registrado. **Sin motivo escrito el botón de confirmar no se habilita**, y por eso la regla
 * es visible antes de intentar en vez de aparecer como rechazo después.
 *
 * **Cancelar no modifica nada**: ni el estado, ni la asignación, ni el historial. El componente sólo
 * avisa y quien lo usa decide.
 *
 * Accesibilidad, igual que el diálogo del Módulo 2: recibe el foco al abrirse, `Escape` equivale a
 * cancelar y el foco vuelve al elemento desde el que se abrió.
 */
export function ConfirmacionAnulacion({ numero, onConfirmar, onCancelar }: Props) {
  const dialogo = useRef<HTMLDivElement>(null)
  const origen = useRef<Element | null>(null)
  const [motivo, setMotivo] = useState('')

  useEffect(() => {
    origen.current = document.activeElement
    dialogo.current?.focus()

    return () => {
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

  const sinMotivo = motivo.trim() === ''

  return (
    <div
      ref={dialogo}
      role="dialog"
      aria-modal="true"
      aria-labelledby="titulo-anulacion"
      tabIndex={-1}
      onKeyDown={alPresionarTecla}
    >
      <h2 id="titulo-anulacion">¿Anular el viaje {numero}?</h2>

      <p>
        Deja de contar como trabajo realizado y su importe no figura en ningún total. El chofer y el
        vehículo quedan libres. Queda registrado con su motivo y no se puede volver atrás.
      </p>

      <label htmlFor="motivo-anulacion">Motivo (obligatorio)</label>
      <textarea
        id="motivo-anulacion"
        maxLength={500}
        required
        value={motivo}
        onChange={(evento) => setMotivo(evento.target.value)}
      />

      <button type="button" onClick={() => onConfirmar(motivo.trim())} disabled={sinMotivo}>
        Anular viaje
      </button>
      <button type="button" onClick={onCancelar}>
        Cancelar
      </button>
    </div>
  )
}
