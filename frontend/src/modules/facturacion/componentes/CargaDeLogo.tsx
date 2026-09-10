import { useRef, useState } from 'react'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { clasesDeBoton, clasesDeFormularioSimple } from '../../../compartido/ui/clases'
import { quitarLogo, subirLogo, type EmpresaEmisora } from '../servicios/servicioEmpresaEmisora'

const MENSAJE_SIN_LOGO =
  'Todavía no hay un logo cargado. Es opcional: las facturas se emiten igual.'

const AYUDA = 'JPG o PNG, hasta 10 MB.'

interface Props {
  /** La configuración completa, no sólo el logo: quitarlo devuelve el resto intacto. */
  empresa: EmpresaEmisora
  /**
   * La fila existe y se puede colgar el logo de ella. Sin los cuatro obligatorios guardados no hay
   * dónde guardarlo, y pedir el logo antes de la razón social sería al revés de como se usa
   * (contracts/README §Logo).
   */
  configuracionCargada: boolean
  onCambio: (empresa: EmpresaEmisora) => void
}

/**
 * El logo de la empresa emisora: zona propia dentro de la misma pantalla (FR-003, FR-004).
 *
 * **Es opcional y la pantalla lo dice con esas palabras**: sin logo las facturas se emiten igual, y
 * dejar el espacio vacío haría pensar que falta cargar algo obligatorio.
 *
 * **Quitar no pide confirmación aparte**: no destruye nada que no se pueda volver a subir, y es el
 * mismo criterio con el que el Módulo 4 trató el alta de un vehículo (precedente [004]). Las bajas que
 * sí confirman son las que dejan de ofrecer algo en el resto del sistema.
 *
 * El archivo **no se valida acá**: el servidor decide por la firma del archivo, no por la extensión ni
 * por el tipo que declara el navegador. Repetir la validación en la pantalla daría una segunda regla
 * que puede discrepar de la real (FR-003).
 */
export function CargaDeLogo({ empresa, configuracionCargada, onCambio }: Props) {
  const logo = empresa.logo
  const entrada = useRef<HTMLInputElement>(null)
  const [trabajando, setTrabajando] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [aviso, setAviso] = useState<string | null>(null)

  async function elegido(archivo: File | undefined) {
    if (archivo === undefined) return

    setTrabajando(true)
    setError(null)
    setAviso(null)

    try {
      onCambio(await subirLogo(archivo))
      setAviso('El logo quedó cargado.')
    } catch (fallo) {
      setError(
        fallo instanceof ErrorHttp
          ? fallo.detalle.mensaje
          : 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.',
      )
    } finally {
      setTrabajando(false)

      // Sin esto, elegir el mismo archivo dos veces seguidas no vuelve a disparar el evento.
      if (entrada.current !== null) entrada.current.value = ''
    }
  }

  async function quitar() {
    setTrabajando(true)
    setError(null)
    setAviso(null)

    try {
      await quitarLogo()

      // El `DELETE` responde 204 sin cuerpo, así que la configuración se actualiza acá: sólo cambia
      // el logo y el resto queda tal como estaba.
      onCambio({ ...empresa, logo: null })
      setAviso('El logo quedó quitado. Las facturas se siguen emitiendo.')
    } catch (fallo) {
      setError(
        fallo instanceof ErrorHttp
          ? fallo.detalle.mensaje
          : 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.',
      )
    } finally {
      setTrabajando(false)
    }
  }

  return (
    <section aria-labelledby="titulo-logo" className={`${clasesDeFormularioSimple} mt-6`}>
      <h2 id="titulo-logo" className="m-0 text-[17px] font-bold tracking-[-0.02em] text-ink">Logo</h2>

      {/* El resultado aparece sin que la pantalla cambie, así que se anuncia (convención [003]). */}
      {aviso !== null && <p role="status" className="m-0 text-[13.5px] font-medium text-brand">{aviso}</p>}
      {error !== null && <p role="alert" className="formulario__error">{error}</p>}

      {logo === null ? (
        <p className="m-0 text-[13.5px] text-ink">{MENSAJE_SIN_LOGO}</p>
      ) : (
        <div className="flex flex-col gap-3 rounded-card border border-line bg-surface-soft p-4">
          <img src={logo.url} alt={`Logo de la empresa emisora: ${logo.nombre}`} height={60} className="w-auto self-start" />
          <p className="m-0 text-[12.5px] font-medium text-ink-soft">{logo.nombre}</p>
        </div>
      )}

      <div className="campo max-w-campo-largo">
        <label htmlFor="logo">{logo === null ? 'Cargar logo' : 'Reemplazar logo'}</label>
        
        <div className="flex flex-wrap items-center gap-4">
          <input
            ref={entrada}
            id="logo"
            type="file"
            accept="image/jpeg,image/png"
            aria-describedby="ayuda-logo"
            disabled={trabajando || !configuracionCargada}
            onChange={(evento) => elegido(evento.target.files?.[0])}
            className="flex-1 cursor-pointer file:mr-4 file:cursor-pointer file:rounded-pastilla file:border-0 file:bg-surface-mute file:px-4 file:py-1.5 file:text-[12.5px] file:font-semibold file:text-ink-soft hover:file:bg-[#E9EAEF]"
          />
          
          {logo !== null && (
            <button 
              type="button" 
              onClick={quitar} 
              disabled={trabajando}
              className={clasesDeBoton('destructivo', 'chico')}
            >
              Quitar
            </button>
          )}
        </div>

        <p id="ayuda-logo" className="m-0 mt-1 text-[11.5px] text-faint">{AYUDA}</p>

        {!configuracionCargada && (
          <p className="m-0 mt-2 text-[12.5px] font-semibold text-danger">Guardá primero los datos de la empresa emisora para poder cargar el logo.</p>
        )}
      </div>
    </section>
  )
}
