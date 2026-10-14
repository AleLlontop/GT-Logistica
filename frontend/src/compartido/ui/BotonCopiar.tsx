import { useState, useCallback } from 'react'
import { IconoCopiar, IconoCopiado } from './iconos'
import { cn } from './cn'

interface Props {
  /** Texto a copiar en el portapapeles. */
  texto: string
  /** Etiqueta accesible para el botón o tooltip. Por defecto: "Copiar al portapapeles". */
  etiqueta?: string
  /** Clases adicionales para el botón contenedor. */
  className?: string
  /** Notificación opcional cuando se completa el copiado. */
  onCopiado?: () => void
}

/**
  * Botón para copiar textos largos (CAE, CUIT, CBU) con un clic (QoL).
  *
  * Provee feedback visual inmediato ("¡Copiado!" con tilde de confirmación) durante 2 segundos
  * y detiene la propagación para evitar activar enlaces o filas contenedoras.
  */
export function BotonCopiar({
  texto,
  etiqueta = 'Copiar al portapapeles',
  className,
  onCopiado,
}: Props) {
  const [copiado, setCopiado] = useState(false)

  const copiar = useCallback(
    async (evento: React.MouseEvent<HTMLButtonElement>) => {
      evento.preventDefault()
      evento.stopPropagation()

      try {
        await navigator.clipboard.writeText(texto)
        setCopiado(true)
        onCopiado?.()

        setTimeout(() => {
          setCopiado(false)
        }, 2000)
      } catch {
        // Fallback para contextos donde navigator.clipboard pueda no estar disponible
        const elementoTemporal = document.createElement('textarea')
        elementoTemporal.value = texto
        document.body.appendChild(elementoTemporal)
        elementoTemporal.select()
        document.execCommand('copy')
        document.body.removeChild(elementoTemporal)

        setCopiado(true)
        onCopiado?.()
        setTimeout(() => setCopiado(false), 2000)
      }
    },
    [texto, onCopiado],
  )

  return (
    <button
      type="button"
      onClick={copiar}
      title={copiado ? '¡Copiado!' : etiqueta}
      aria-label={copiado ? '¡Copiado!' : etiqueta}
      className={cn(
        'inline-flex items-center gap-1 rounded-pastilla px-1.5 py-0.5 text-[11px] font-medium transition-all duration-150 cursor-pointer select-none',
        copiado
          ? 'bg-estado-rendido-bg text-estado-rendido font-semibold'
          : 'text-faint hover:bg-surface-mute hover:text-ink',
        className,
      )}
    >
      {copiado ? (
        <>
          <IconoCopiado className="size-3 text-estado-rendido" aria-hidden="true" />
          <span>¡Copiado!</span>
        </>
      ) : (
        <>
          <IconoCopiar className="size-3" aria-hidden="true" />
          <span className="sr-only">{etiqueta}</span>
        </>
      )}
    </button>
  )
}
