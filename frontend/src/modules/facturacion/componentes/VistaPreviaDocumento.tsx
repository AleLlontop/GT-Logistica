import { useEffect, useState } from 'react'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { pedirVistaPrevia, type EmisionPeticion } from '../servicios/servicioFacturas'

export const ADVERTENCIA_VISTA_PREVIA =
  'Así va a salir la factura. Revisala antes de confirmar: una vez emitida, el cliente, los viajes y ' +
  'los importes no se pueden cambiar.'

interface Props {
  /** `null` mientras el formulario no esté completo: sin datos no hay nada que previsualizar. */
  peticion: EmisionPeticion | null
}

/**
 * Bloque 4 del alta: el documento tal como va a quedar (FR-033).
 *
 * **No es una maqueta dibujada en React**, y ahí está todo el punto. Es el mismo PDF que se va a
 * guardar, pedido a `POST /api/facturas/vista-previa` y mostrado en un `<iframe>` sobre una URL de
 * `Blob`. Si esta pantalla dibujara algo parecido, las dos maquetas se separarían sin que nadie lo note
 * y revisar la vista previa dejaría de servir para algo (research §2).
 *
 * Es un patrón nuevo en este frontend, y por eso está anotado: el resto del sistema muestra JSON.
 *
 * Pedirla no crea la factura ni guarda ningún archivo; abandonar la pantalla no deja rastro
 * (US2 esc. 33).
 */
export function VistaPreviaDocumento({ peticion }: Props) {
  const [url, setUrl] = useState<string | null>(null)
  const [cargando, setCargando] = useState(false)
  const [error, setError] = useState<string | null>(null)

  // La URL de `Blob` vive en la memoria del navegador hasta que se la revoca. Sin esta limpieza, cada
  // vista previa dejaría una copia del PDF retenida mientras la pestaña siga abierta.
  useEffect(() => () => {
    if (url !== null) URL.revokeObjectURL(url)
  }, [url])

  async function pedir() {
    if (peticion === null) return

    setCargando(true)
    setError(null)

    try {
      const pdf = await pedirVistaPrevia(peticion)

      setUrl((anterior) => {
        if (anterior !== null) URL.revokeObjectURL(anterior)

        return URL.createObjectURL(pdf)
      })
    } catch (fallo) {
      setError(
        fallo instanceof ErrorHttp
          ? fallo.detalle.mensaje
          : 'No pudimos generar la vista previa. Volvé a intentar en unos minutos.',
      )
    } finally {
      setCargando(false)
    }
  }

  return (
    <section aria-labelledby="titulo-vista-previa">
      <h2 id="titulo-vista-previa">Vista previa</h2>

      <button type="button" onClick={pedir} disabled={peticion === null || cargando}>
        Ver vista previa
      </button>

      {peticion === null && (
        <p>Completá los datos del comprobante y elegí al menos un viaje para poder previsualizar.</p>
      )}

      {cargando && <p role="status">Generando la vista previa…</p>}
      {error !== null && <p role="alert">{error}</p>}

      {url !== null && (
        <>
          <p role="status">{ADVERTENCIA_VISTA_PREVIA}</p>

          <iframe
            src={url}
            title="Vista previa del documento de la factura"
            width="100%"
            height="600"
          />
        </>
      )}
    </section>
  )
}
