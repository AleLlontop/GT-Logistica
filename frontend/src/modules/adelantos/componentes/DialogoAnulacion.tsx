import { useState } from 'react'
import { formatearPesos } from '../../../compartido/moneda'
import { BarraDeAcciones } from '../../../compartido/ui/BarraDeAcciones'
import { Boton } from '../../../compartido/ui/Boton'
import { clasesDeAvisoDePantalla, clasesDeFormulario } from '../../../compartido/ui/clases'
import { cn } from '../../../compartido/ui/cn'
import { Dialogo } from '../../../compartido/ui/Dialogo'
import { IconoAnulado } from '../../../compartido/ui/iconos'
import { mensajeDeRechazo } from '../servicios/servicioAdelantos'

/** Largo máximo del motivo (contracts/README §Anular adelanto). */
export const LARGO_MAXIMO_DEL_MOTIVO = 500

interface Props {
  /** `Pérez, Juan`. */
  persona: string
  importe: number
  /** Lanza el rechazo del servidor, que el diálogo muestra adentro. */
  onAnular: (motivo: string) => Promise<void>
  onCancelar: () => void
}

/**
 * Anular un adelanto aprobado (User Story 5, FR-029 a FR-031).
 *
 * Se llama igual que el del Módulo 9 y **no se comparte**: el texto, el cuerpo y el resultado son del
 * adelanto, y lo común —superficie, foco, `Escape`— ya lo da `Dialogo` (convención [007]).
 *
 * **Este diálogo es la confirmación explícita** y el envío lleva `confirmado: true` (research §4). Por eso
 * **no hay primario**: el destructivo es la única decisión.
 */
export function DialogoAnulacion({ persona, importe, onAnular, onCancelar }: Props) {
  const [motivo, setMotivo] = useState('')
  const [tocado, setTocado] = useState(false)
  const [intento, setIntento] = useState(false)
  const [enviando, setEnviando] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const errorDeMotivo =
    (tocado || intento) && motivo.trim() === '' ? 'Escribí el motivo de la anulación: queda en el historial.' : null

  async function anular() {
    setIntento(true)

    if (motivo.trim() === '') {
      return
    }

    setEnviando(true)
    setError(null)

    try {
      await onAnular(motivo.trim())
    } catch (fallo) {
      setError(mensajeDeRechazo(fallo))
      setEnviando(false)
    }
  }

  return (
    <Dialogo titulo="Anular adelanto" onCerrar={onCancelar}>
      <p className="mt-3 mb-0 text-[13px] leading-5 text-ink-soft">
        El adelanto de <strong>{formatearPesos(importe)}</strong> para <strong>{persona}</strong> queda anulado
        y deja de sumar en el total adelantado. No se puede deshacer.
      </p>

      {error !== null && (
        <p role="alert" className={cn(clasesDeAvisoDePantalla.error, 'mt-4')}>
          {error}
        </p>
      )}

      <div className={cn(clasesDeFormulario, 'mt-5')}>
        <div className={cn('campo w-full', errorDeMotivo !== null && 'con-error')}>
          <label htmlFor="motivoAnulacionAdelanto">Motivo</label>
          <textarea
            id="motivoAnulacionAdelanto"
            required
            rows={3}
            maxLength={LARGO_MAXIMO_DEL_MOTIVO}
            placeholder="Cargado sobre la persona equivocada."
            value={motivo}
            onChange={(evento) => setMotivo(evento.target.value)}
            onBlur={() => setTocado(true)}
            aria-invalid={errorDeMotivo !== null}
            aria-describedby={errorDeMotivo !== null ? 'error-motivoAnulacionAdelanto' : undefined}
          />
          {errorDeMotivo !== null && (
            <p className="campo__error" id="error-motivoAnulacionAdelanto">
              {errorDeMotivo}
            </p>
          )}
        </div>

        <BarraDeAcciones anclaje="contenedor">
          <Boton variante="secundario" onClick={onCancelar} disabled={enviando}>
            Volver
          </Boton>
          <Boton
            variante="destructivo"
            onClick={() => void anular()}
            disabled={enviando}
            icono={<IconoAnulado className="size-3" />}
          >
            Anular adelanto
          </Boton>
        </BarraDeAcciones>
      </div>
    </Dialogo>
  )
}
