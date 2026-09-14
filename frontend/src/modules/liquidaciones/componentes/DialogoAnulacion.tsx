import { useState } from 'react'
import { BarraDeAcciones } from '../../../compartido/ui/BarraDeAcciones'
import { Boton } from '../../../compartido/ui/Boton'
import { clasesDeAvisoDePantalla, clasesDeFormulario } from '../../../compartido/ui/clases'
import { cn } from '../../../compartido/ui/cn'
import { Dialogo } from '../../../compartido/ui/Dialogo'
import { IconoAnulado } from '../../../compartido/ui/iconos'
import { detalleDeError } from '../servicios/servicioLiquidaciones'

/** Largo máximo del motivo (FR-055, contracts/README §Anular liquidación). */
export const LARGO_MAXIMO_DEL_MOTIVO = 500

const MENSAJE_INESPERADO = 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.'

interface Props {
  numero: string
  cantidadDeViajes: number
  /** Lanza el rechazo del servidor, que el diálogo muestra adentro. */
  onAnular: (motivo: string) => Promise<void>
  onCancelar: () => void
}

/**
 * Anular una liquidación (User Story 6).
 *
 * **Este diálogo es la confirmación explícita**: pide el motivo y su botón nombra la consecuencia, y el
 * envío lleva `confirmado: true` (research §7). Por eso **no hay primario**: el destructivo ocupa su lugar
 * y ofrecer otro al lado sería competir con la única decisión del diálogo.
 *
 * El botón no se deshabilita sin motivo: apretarlo marca el campo con el error, que dice qué hacer
 * (quickstart paso 26). *Volver* no llama al backend.
 */
export function DialogoAnulacion({ numero, cantidadDeViajes, onAnular, onCancelar }: Props) {
  const [motivo, setMotivo] = useState('')
  const [tocado, setTocado] = useState(false)
  const [intento, setIntento] = useState(false)
  const [enviando, setEnviando] = useState(false)
  const [error, setError] = useState<string | null>(null)

  const errorDeMotivo =
    (tocado || intento) && motivo.trim() === ''
      ? 'Escribí el motivo de la anulación: queda en el historial.'
      : null

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
      setError(detalleDeError(fallo)?.mensaje ?? MENSAJE_INESPERADO)
      setEnviando(false)
    }
  }

  return (
    <Dialogo titulo={`Anular liquidación ${numero}`} onCerrar={onCancelar}>
      <p className="mt-3 mb-0 text-[13px] leading-5 text-ink-soft">
        {cantidadDeViajes === 1
          ? 'La liquidación queda anulada y su viaje vuelve a estar disponible para liquidarse.'
          : `La liquidación queda anulada y sus ${cantidadDeViajes} viajes vuelven a estar disponibles para liquidarse.`}{' '}
        No se puede deshacer.
      </p>

      {error !== null && (
        <p role="alert" className={cn(clasesDeAvisoDePantalla.error, 'mt-4')}>
          {error}
        </p>
      )}

      <div className={cn(clasesDeFormulario, 'mt-5')}>
        <div className={cn('campo w-full', errorDeMotivo !== null && 'con-error')}>
          <label htmlFor="motivoAnulacion">Motivo</label>
          <textarea
            id="motivoAnulacion"
            required
            rows={3}
            maxLength={LARGO_MAXIMO_DEL_MOTIVO}
            placeholder="El período no correspondía: los viajes eran de junio."
            value={motivo}
            onChange={(evento) => setMotivo(evento.target.value)}
            onBlur={() => setTocado(true)}
            aria-invalid={errorDeMotivo !== null}
            aria-describedby={errorDeMotivo !== null ? 'error-motivoAnulacion' : undefined}
          />
          {errorDeMotivo !== null && (
            <p className="campo__error" id="error-motivoAnulacion">
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
            Anular liquidación
          </Boton>
        </BarraDeAcciones>
      </div>
    </Dialogo>
  )
}
