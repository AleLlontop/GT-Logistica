import { Dialogo } from '../../../compartido/ui/Dialogo'
import { useState } from 'react'

/** Largo máximo del motivo (FR-046, contracts/README §Anular una factura). */
export const LARGO_MAXIMO_DEL_MOTIVO = 500

interface Props {
  numero: string
  cantidadDeViajes: number
  trabajando: boolean
  onConfirmar: (motivo: string) => void
  onCancelar: () => void
}

/**
 * Confirmación de la anulación de una factura (FR-046 a FR-048).
 *
 * **El botón no se habilita sin motivo escrito**, y ése es el requisito: el motivo es un dato obligatorio,
 * no una formalidad. Queda impreso en el documento regenerado y visible en la ficha y en el listado
 * (FR-031d).
 *
 * **La confirmación la pide la pantalla y no el backend**, a diferencia de las dos de la emisión: la
 * anulación es un cambio de estado con motivo, y su irreversibilidad ya está cubierta por el motivo
 * obligatorio — quien escribe por qué anula ya pensó lo que está haciendo (research §11).
 *
 * **Cancelar no modifica nada**, y eso empieza por no llamar al backend (US6 esc. 3).
 */
export function ConfirmacionAnulacion({
  numero,
  cantidadDeViajes,
  trabajando,
  onConfirmar,
  onCancelar,
}: Props) {
  const [motivo, setMotivo] = useState('')

  const puedeAnular = motivo.trim() !== '' && motivo.length <= LARGO_MAXIMO_DEL_MOTIVO

  return (
    <Dialogo titulo={`¿Anular la factura ${numero}?`} onCerrar={onCancelar}>

      <p>
        Sus {cantidadDeViajes} viajes vuelven a estado rendido y quedan disponibles para facturar de
        nuevo. La factura queda anulada y su documento se regenera indicando el motivo. No se puede
        deshacer.
      </p>

      <div className="campo">
        <label htmlFor="motivoAnulacion">
          Motivo de la anulación (obligatorio, hasta {LARGO_MAXIMO_DEL_MOTIVO} caracteres)
        </label>
        <textarea
          id="motivoAnulacion"
          required
          maxLength={LARGO_MAXIMO_DEL_MOTIVO}
          value={motivo}
          onChange={(evento) => setMotivo(evento.target.value)}
        />
      </div>

      <div className="acciones">
        <button type="button" onClick={onCancelar} disabled={trabajando}>
          Cancelar
        </button>

        {/* Deshabilitado sin motivo, que es el requisito y no una cortesía (FR-046). */}
        <button
          type="button"
          onClick={() => onConfirmar(motivo.trim())}
          disabled={trabajando || !puedeAnular}
        >
          Anular factura
        </button>
      </div>
    </Dialogo>
  )
}
