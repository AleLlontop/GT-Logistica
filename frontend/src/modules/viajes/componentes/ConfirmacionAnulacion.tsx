import { Dialogo } from '../../../compartido/ui/Dialogo'
import { useState } from 'react'

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
  const [motivo, setMotivo] = useState('')



  const sinMotivo = motivo.trim() === ''

  return (
    <Dialogo titulo={`¿Anular el viaje ${numero}?`} onCerrar={onCancelar}>

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
    </Dialogo>
  )
}
