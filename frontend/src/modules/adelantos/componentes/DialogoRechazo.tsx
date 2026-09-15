import { useState } from 'react'
import { formatearPesos } from '../../../compartido/moneda'
import { BarraDeAcciones } from '../../../compartido/ui/BarraDeAcciones'
import { Boton } from '../../../compartido/ui/Boton'
import { clasesDeAvisoDePantalla, clasesDeFormulario } from '../../../compartido/ui/clases'
import { cn } from '../../../compartido/ui/cn'
import { Dialogo } from '../../../compartido/ui/Dialogo'
import { mensajeDeRechazo } from '../servicios/servicioAdelantos'

/** Largo máximo del motivo (contracts/README §Rechazar adelanto). */
export const LARGO_MAXIMO_DEL_MOTIVO = 500

interface Props {
  /** `Pérez, Juan`. */
  persona: string
  importe: number
  /** Lanza el rechazo del servidor, que el diálogo muestra adentro. */
  onRechazar: (motivo: string) => Promise<void>
  onCancelar: () => void
}

/**
 * Rechazar un adelanto pendiente (User Story 4, FR-024, FR-025).
 *
 * **Este diálogo es la confirmación explícita**: pide el motivo y su botón nombra la consecuencia, y el
 * envío lleva `confirmado: true` (research §4).
 *
 * **Rechazar no es destructivo**, aunque no se deshaga: no borra ni revierte nada firme, porque el dinero
 * nunca contó como adelantado. Por eso el botón es primario y no rojo (plan §UI Design Check).
 *
 * El botón no se deshabilita sin motivo: apretarlo marca el campo con el error, que dice qué hacer.
 * *Volver* no llama al backend.
 */
export function DialogoRechazo({ persona, importe, onRechazar, onCancelar }: Props) {
  const [motivo, setMotivo] = useState('')
  const [tocado, setTocado] = useState(false)
  const [intento, setIntento] = useState(false)
  const [enviando, setEnviando] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const errorDeMotivo =
    (tocado || intento) && motivo.trim() === '' ? 'Escribí el motivo del rechazo: queda en el historial.' : null

  async function rechazar() {
    setIntento(true)

    if (motivo.trim() === '') {
      return
    }

    setEnviando(true)
    setError(null)

    try {
      await onRechazar(motivo.trim())
    } catch (fallo) {
      setError(mensajeDeRechazo(fallo))
      setEnviando(false)
    }
  }

  return (
    <Dialogo titulo="Rechazar adelanto" onCerrar={onCancelar}>
      <p className="m-0 mt-1 text-[12.5px] text-faint">
        {persona} · {formatearPesos(importe)}
      </p>

      <p className="mt-3 mb-0 text-[13px] leading-5 text-ink-soft">
        El adelanto queda rechazado y no se vuelve a presentar. Si hace falta, se registra uno nuevo. No se
        puede deshacer.
      </p>

      {error !== null && (
        <p role="alert" className={cn(clasesDeAvisoDePantalla.error, 'mt-4')}>
          {error}
        </p>
      )}

      <div className={cn(clasesDeFormulario, 'mt-5')}>
        <div className={cn('campo w-full', errorDeMotivo !== null && 'con-error')}>
          <label htmlFor="motivoRechazo">Motivo</label>
          <textarea
            id="motivoRechazo"
            required
            rows={3}
            maxLength={LARGO_MAXIMO_DEL_MOTIVO}
            placeholder="Ya tiene un adelanto pendiente del mes anterior."
            value={motivo}
            onChange={(evento) => setMotivo(evento.target.value)}
            onBlur={() => setTocado(true)}
            aria-invalid={errorDeMotivo !== null}
            aria-describedby={errorDeMotivo !== null ? 'error-motivoRechazo' : undefined}
          />
          {errorDeMotivo !== null && (
            <p className="campo__error" id="error-motivoRechazo">
              {errorDeMotivo}
            </p>
          )}
        </div>

        <BarraDeAcciones anclaje="contenedor">
          <Boton variante="secundario" onClick={onCancelar} disabled={enviando}>
            Volver
          </Boton>
          <Boton variante="primario" onClick={() => void rechazar()} disabled={enviando}>
            Rechazar adelanto
          </Boton>
        </BarraDeAcciones>
      </div>
    </Dialogo>
  )
}
