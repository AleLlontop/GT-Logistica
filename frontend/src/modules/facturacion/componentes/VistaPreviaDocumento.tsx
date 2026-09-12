import { useEffect, useState } from 'react'
import { Boton } from '../../../compartido/ui/Boton'
import { SeccionNumerada } from '../../../compartido/ui/SeccionNumerada'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { pedirVistaPrevia, type EmisionPeticion } from '../servicios/servicioFacturas'

export const ADVERTENCIA_VISTA_PREVIA =
  'Así va a salir la factura. Revisala antes de confirmar: una vez emitida, el cliente, los viajes y ' +
  'los importes no se pueden cambiar.'

interface Props {
  /** Su lugar en el orden de lectura del alta. */
  numero: number
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
export function VistaPreviaDocumento({ numero, peticion }: Props) {
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
    <SeccionNumerada numero={numero} titulo="Vista previa">
      <div className="flex flex-wrap items-center gap-3">
        <Boton variante="secundario" onClick={pedir} disabled={peticion === null || cargando}>
          Ver vista previa
        </Boton>

        {peticion === null && (
          <p className="m-0 text-[12.5px] text-faint">
            Completá los datos del comprobante y elegí al menos un viaje para poder previsualizar.
          </p>
        )}
      </div>

      {/*
        Las dos regiones se dibujan siempre: una que entra al DOM junto con su texto no se anuncia.
        Vacías quedan `sr-only`, que sigue en el árbol accesible y, por ser absoluta, no ocupa un
        hueco de la sección. El `role` va sobre el `<p>` del mensaje, no sobre lo que lo enmarque.
      */}
      <p
        role="status"
        className={cargando ? 'm-0 text-[13px] text-ink-soft' : 'sr-only'}
      >
        {cargando ? 'Generando la vista previa…' : null}
      </p>
      <p role="alert" className={error === null ? 'sr-only' : 'formulario__error'}>
        {error}
      </p>

      {url !== null && (
        <>
          <p
            role="status"
            className="m-0 rounded-card border border-line bg-estado-pendiente-bg px-[18px] py-4 text-[13px] leading-5 font-medium text-estado-pendiente"
          >
            {ADVERTENCIA_VISTA_PREVIA}
          </p>

          <iframe
            src={url}
            title="Vista previa del documento de la factura"
            width="100%"
            height="600"
            className="rounded-card border border-line bg-surface-soft"
          />
        </>
      )}
    </SeccionNumerada>
  )
}
