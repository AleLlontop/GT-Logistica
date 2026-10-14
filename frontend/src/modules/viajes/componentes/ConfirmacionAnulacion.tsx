import { BarraDeAcciones } from '../../../compartido/ui/BarraDeAcciones'
import { Boton } from '../../../compartido/ui/Boton'
import { Dialogo } from '../../../compartido/ui/Dialogo'
import { IconoAnulado } from '../../../compartido/ui/iconos'
import { cn } from '../../../compartido/ui/cn'
import { clasesDeFormulario } from '../../../compartido/ui/clases'
import { useState } from 'react'

export const LARGO_MAXIMO_DEL_MOTIVO = 500

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

  const sinMotivo = motivo.trim() === '' || motivo.length > LARGO_MAXIMO_DEL_MOTIVO

  return (
    <Dialogo titulo={`¿Anular el viaje ${numero}?`} onCerrar={onCancelar}>
      <p className="mt-3 mb-0 text-[13px] leading-5 text-ink-soft">
        Deja de contar como trabajo realizado y su importe no figura en ningún total. El chofer y el
        vehículo quedan libres. Queda registrado con su motivo y no se puede volver atrás.
      </p>

      <div className={cn(clasesDeFormulario, 'mt-5')}>
        <div className="campo w-full">
          <label htmlFor="motivo-anulacion">Motivo (obligatorio)</label>
          <textarea
            id="motivo-anulacion"
            maxLength={LARGO_MAXIMO_DEL_MOTIVO}
            required
            rows={3}
            placeholder="Describí el motivo por el cual se anula el viaje..."
            value={motivo}
            onChange={(evento) => setMotivo(evento.target.value)}
            className="w-full resize-y min-h-[84px]"
          />
        </div>

        <BarraDeAcciones anclaje="contenedor">
          <Boton variante="secundario" onClick={onCancelar}>
            Cancelar
          </Boton>
          <Boton
            variante="destructivo"
            onClick={() => onConfirmar(motivo.trim())}
            disabled={sinMotivo}
            icono={<IconoAnulado className="size-3" />}
          >
            Anular viaje
          </Boton>
        </BarraDeAcciones>
      </div>
    </Dialogo>
  )
}
