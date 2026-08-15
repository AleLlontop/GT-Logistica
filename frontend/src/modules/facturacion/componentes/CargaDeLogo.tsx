import { useRef, useState } from 'react'
import { ErrorHttp } from '../../../compartido/clienteHttp'
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
    <section aria-labelledby="titulo-logo">
      <h2 id="titulo-logo">Logo</h2>

      {/* El resultado aparece sin que la pantalla cambie, así que se anuncia (convención [003]). */}
      {aviso !== null && <p role="status">{aviso}</p>}
      {error !== null && <p role="alert">{error}</p>}

      {logo === null ? (
        <p>{MENSAJE_SIN_LOGO}</p>
      ) : (
        <div>
          <img src={logo.url} alt={`Logo de la empresa emisora: ${logo.nombre}`} height={60} />
          <p>{logo.nombre}</p>
        </div>
      )}

      <div className="campo">
        <label htmlFor="logo">{logo === null ? 'Cargar logo' : 'Reemplazar logo'}</label>
        <input
          ref={entrada}
          id="logo"
          type="file"
          accept="image/jpeg,image/png"
          aria-describedby="ayuda-logo"
          disabled={trabajando || !configuracionCargada}
          onChange={(evento) => elegido(evento.target.files?.[0])}
        />
        <p id="ayuda-logo">{AYUDA}</p>

        {!configuracionCargada && (
          <p>Guardá primero los datos de la empresa emisora para poder cargar el logo.</p>
        )}
      </div>

      {logo !== null && (
        <button type="button" onClick={quitar} disabled={trabajando}>
          Quitar
        </button>
      )}
    </section>
  )
}
