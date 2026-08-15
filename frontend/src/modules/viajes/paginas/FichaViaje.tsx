import { useCallback, useEffect, useState } from 'react'
import { Link, useLocation, useNavigate, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { formatearFecha, formatearInstante } from '../../../compartido/fechas'
import { formatearPesos } from '../../../compartido/moneda'
import { ConfirmacionAnulacion } from '../componentes/ConfirmacionAnulacion'
import {
  ConfirmacionRendicion,
  MENSAJE_REMITO_REQUERIDO,
} from '../componentes/ConfirmacionRendicion'
import { CodigosErrorViajes, esErrorDeViajes } from '../servicios/api'
import {
  anularViaje,
  leyendaDeFactura,
  nombreConEstado,
  NOMBRES_DE_ESTADO,
  obtenerViaje,
  ponerViajeEnCurso,
  rendirViaje,
  type Advertencia,
  type ViajeDetalle,
} from '../servicios/servicioViajes'

const MENSAJE_VIAJE_RENDIDO =
  'Este viaje está rendido. Los viajes rendidos no se editan, no se reasignan y no se anulan.'
const MENSAJE_VIAJE_ANULADO =
  'Este viaje está anulado. No se edita, no se reasigna y no se puede volver atrás.'

/**
 * Módulo 6, FR-052. Dice **dónde** mirar para destrabarlo: sin la mención a la factura, quien opera ve
 * una ficha sin botones y no sabe qué hacer al respecto.
 */
const MENSAJE_VIAJE_FACTURADO =
  'Este viaje está facturado. No se edita, no se reasigna y no cambia de estado. Anulá la factura si ' +
  'necesitás corregirlo.'
const MENSAJE_FALTA_ASIGNAR = 'Asigná chofer y vehículo antes de poner el viaje en curso.'
const MENSAJE_ERROR_GENERICO = 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.'

interface Props {
  /**
   * `viajes.gestionar`. Quien sólo consulta ve la ficha completa y ninguna acción de escritura
   * (FR-052). Ocultarlas es una cortesía: invocarlas a mano igual devuelve 403 (SC-012).
   */
  puedeGestionar: boolean
}

/**
 * Ficha completa de un viaje (FR-045), con su historial de cambios de estado (FR-035).
 *
 * El historial se lee de la línea más vieja a la más nueva y la del alta se muestra como `Alta`: no
 * tiene estado anterior porque antes del alta no había estado.
 *
 * **La pantalla ofrece exactamente las acciones que el estado admite y ninguna más** (FR-018,
 * FR-020, contracts/README.md).
 */
export function FichaViaje({ puedeGestionar }: Props) {
  const { id } = useParams()
  const navegar = useNavigate()
  const viajeId = Number(id)

  const ubicacion = useLocation()

  const [viaje, setViaje] = useState<ViajeDetalle | null>(null)
  const [error, setError] = useState<string | null>(null)

  // El alta y la edición confirman acá, porque el guardado ocurrió en el formulario y la navegación
  // se llevó el resultado con ella (convención [003]). Las advertencias que no bloquean —origen igual
  // a destino, carga retroactiva— llegan por el mismo camino y se anuncian junto a la confirmación,
  // sin pedir ningún paso extra (FR-015a).
  const estadoDeNavegacion = ubicacion.state as {
    aviso?: string
    advertencias?: Advertencia[]
  } | null

  const [aviso, setAviso] = useState<string | null>(estadoDeNavegacion?.aviso ?? null)
  const [advertencias, setAdvertencias] = useState<Advertencia[]>(
    estadoDeNavegacion?.advertencias ?? [],
  )
  const [confirmandoRendicion, setConfirmandoRendicion] = useState(false)
  const [confirmandoAnulacion, setConfirmandoAnulacion] = useState(false)

  /** Módulo 6, FR-055a: el intento de rendir se rechazó porque falta el número de remito. */
  const [faltaRemito, setFaltaRemito] = useState(false)

  const traer = useCallback(() => {
    obtenerViaje(viajeId)
      .then((viaje) => {
        setViaje(viaje)
        setError(null)
      })
      .catch(() => setError('No pudimos traer el viaje. Volvé a intentar en unos minutos.'))
  }, [viajeId])

  useEffect(() => {
    traer()
  }, [traer])

  function mostrarFallo(fallo: unknown) {
    setError(fallo instanceof ErrorHttp ? fallo.detalle.mensaje : MENSAJE_ERROR_GENERICO)
  }

  // Toda acción sobre la ficha limpia lo anterior: la advertencia del alta habla del guardado que la
  // trajo, no del cambio de estado que se acaba de pedir.
  function limpiarAnuncios() {
    setError(null)
    setAviso(null)
    setAdvertencias([])
  }

  async function ponerEnCurso() {
    limpiarAnuncios()

    try {
      const actualizado = await ponerViajeEnCurso(viajeId)
      setViaje(actualizado)
      setAviso(`El viaje ${actualizado.numero} está en curso.`)
    } catch (fallo) {
      // Acá caen los rechazos de FR-025 y FR-026: falta asignar, unidad dada de baja, unidad ocupada
      // por otro viaje —con el número del que la ocupa, ya escrito en el mensaje—.
      mostrarFallo(fallo)
    }
  }

  /**
   * Con importe en cero el backend responde `409` sin cambiar nada y recién entonces se abre el
   * diálogo. La pantalla no adivina: pregunta y reacciona a lo que el servidor decide (FR-038).
   */
  async function rendir(confirmado = false) {
    limpiarAnuncios()
    setFaltaRemito(false)

    try {
      const actualizado = await rendirViaje(viajeId, confirmado)
      setViaje(actualizado)
      setConfirmandoRendicion(false)
      setAviso(`El viaje ${actualizado.numero} quedó rendido.`)
    } catch (fallo) {
      if (esErrorDeViajes(fallo, CodigosErrorViajes.rendicionRequiereConfirmacion)) {
        setConfirmandoRendicion(true)
        return
      }

      setConfirmandoRendicion(false)

      // Módulo 6, FR-055a. **No abre diálogo**: es un dato que falta, no un aviso que se confirma. Se
      // muestra el rechazo del servidor y se ofrece el camino para resolverlo, que es la pantalla de
      // edición — el remito no se carga desde la ficha.
      if (esErrorDeViajes(fallo, CodigosErrorViajes.remitoRequerido)) {
        setFaltaRemito(true)
      }

      mostrarFallo(fallo)
    }
  }

  async function anular(motivo: string) {
    limpiarAnuncios()

    try {
      const actualizado = await anularViaje(viajeId, motivo)
      setViaje(actualizado)
      setAviso(`El viaje ${actualizado.numero} quedó anulado.`)
    } catch (fallo) {
      mostrarFallo(fallo)
    } finally {
      setConfirmandoAnulacion(false)
    }
  }

  if (error !== null) {
    return (
      <main>
        <h1>Viaje</h1>
        <p role="alert">{error}</p>
      </main>
    )
  }

  if (viaje === null) {
    return (
      <main>
        <h1>Viaje</h1>
        <p role="status">Cargando…</p>
      </main>
    )
  }

  // Los dos estados que admiten escritura. Los terminales no ofrecen ninguna acción (FR-018).
  const enCurso = viaje.estado === 'pendiente' || viaje.estado === 'enCurso'
  const faltaAsignar = viaje.chofer === null || viaje.vehiculo === null

  return (
    <main>
      <h1>Viaje {viaje.numero}</h1>

      {error !== null && <p role="alert">{error}</p>}
      {aviso !== null && <p role="status">{aviso}</p>}

      {advertencias.map((advertencia) => (
        <p key={advertencia.codigo} role="status">
          {advertencia.mensaje}
        </p>
      ))}

      {/* En los dos estados terminales la ficha lo dice, para que no parezca que faltan botones
          (FR-018, contracts/README.md). */}
      {viaje.estado === 'rendido' && <p role="status">{MENSAJE_VIAJE_RENDIDO}</p>}
      {viaje.estado === 'anulado' && <p role="status">{MENSAJE_VIAJE_ANULADO}</p>}
      {viaje.estado === 'facturado' && <p role="status">{MENSAJE_VIAJE_FACTURADO}</p>}

      <dl>
        <dt>Cliente</dt>
        <dd>{nombreConEstado(viaje.cliente)}</dd>

        <dt>Fecha</dt>
        <dd>
          {formatearFecha(viaje.fecha)}
          {/* La señal lleva la palabra que la explica, no sólo un color (FR-016, FR-049). */}
          {viaje.esRetroactivo && ' — Carga retroactiva'}
        </dd>

        <dt>Origen</dt>
        <dd>{viaje.origen}</dd>

        <dt>Destino</dt>
        <dd>{viaje.destino}</dd>

        <dt>Número de remito</dt>
        <dd>{viaje.numeroRemito ?? '—'}</dd>

        <dt>Detalle de la carga</dt>
        <dd>{viaje.detalleCarga ?? '—'}</dd>

        <dt>Importe</dt>
        <dd>{formatearPesos(viaje.importe)}</dd>

        <dt>Estado</dt>
        <dd>
          {NOMBRES_DE_ESTADO[viaje.estado]}
          {viaje.demorado && ' — Demorado'}
        </dd>

        <dt>Chofer</dt>
        <dd>{nombreConEstado(viaje.chofer)}</dd>

        <dt>Vehículo</dt>
        <dd>{nombreConEstado(viaje.vehiculo)}</dd>

        <dt>Transportista</dt>
        <dd>{nombreConEstado(viaje.transportista)}</dd>

        {/* Módulo 6, FR-055: el número y la fecha de la factura, con enlace a su ficha. Sale de la
            navegación del backend, no de columnas copiadas al viaje. */}
        {viaje.factura && (
          <>
            <dt>Factura</dt>
            <dd>
              <Link to={`/facturas/${viaje.factura.id}`}>
                {leyendaDeFactura(viaje.factura, formatearFecha(viaje.factura.fecha))}
              </Link>
            </dd>
          </>
        )}

        {viaje.motivoAnulacion !== null && (
          <>
            <dt>Motivo de la anulación</dt>
            <dd>{viaje.motivoAnulacion}</dd>
          </>
        )}
      </dl>

      <section>
        <h2>Historial</h2>

        <table>
          <caption>Cambios de estado del viaje {viaje.numero}</caption>
          <thead>
            <tr>
              <th scope="col">Estado anterior</th>
              <th scope="col">Estado nuevo</th>
              <th scope="col">Usuario</th>
              <th scope="col">Cuándo</th>
            </tr>
          </thead>
          <tbody>
            {viaje.historial.map((cambio, indice) => (
              <tr key={`${cambio.ocurridoEn}-${indice}`}>
                <td>
                  {cambio.estadoAnterior === null
                    ? 'Alta'
                    : NOMBRES_DE_ESTADO[cambio.estadoAnterior]}
                </td>
                <td>{NOMBRES_DE_ESTADO[cambio.estadoNuevo]}</td>
                <td>{cambio.usuario}</td>
                <td>{formatearInstante(cambio.ocurridoEn)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>

      {/* La pantalla ofrece exactamente las acciones que el estado admite y ninguna más: en
          `rendido` y en `anulado` no hay ninguna (contracts/README.md). */}
      <div className="acciones">
        {puedeGestionar && enCurso && (
          <>
            <button type="button" onClick={() => navegar(`/viajes/${viaje.id}/editar`)}>
              Editar
            </button>

            <button type="button" onClick={() => navegar(`/viajes/${viaje.id}/asignacion`)}>
              {viaje.chofer === null
                ? 'Asignar chofer y vehículo'
                : 'Reasignar chofer y vehículo'}
            </button>

            {viaje.estado === 'pendiente' && (
              <>
                {/* Deshabilitado **con el motivo a la vista**, no en silencio (FR-025). */}
                <button type="button" onClick={ponerEnCurso} disabled={faltaAsignar}>
                  Poner en curso
                </button>
                {faltaAsignar && <p role="status">{MENSAJE_FALTA_ASIGNAR}</p>}
              </>
            )}

            {viaje.estado === 'enCurso' && (
              <>
                <button type="button" onClick={() => rendir()}>
                  Rendir
                </button>

                {/* Módulo 6, FR-055a. El mensaje del rechazo ya se muestra arriba; acá va el camino
                    para resolverlo, porque el remito se carga en la pantalla de edición y no en la
                    ficha. */}
                {faltaRemito && (
                  <p role="status">
                    {MENSAJE_REMITO_REQUERIDO}{' '}
                    <button type="button" onClick={() => navegar(`/viajes/${viaje.id}/editar`)}>
                      Cargar el remito
                    </button>
                  </p>
                )}
              </>
            )}

            <button type="button" onClick={() => setConfirmandoAnulacion(true)}>
              Anular
            </button>
          </>
        )}

        <button type="button" onClick={() => navegar('/viajes')}>
          Volver al listado
        </button>
      </div>

      {confirmandoRendicion && (
        <ConfirmacionRendicion
          numero={viaje.numero}
          onConfirmar={() => rendir(true)}
          onCancelar={() => setConfirmandoRendicion(false)}
        />
      )}

      {confirmandoAnulacion && (
        <ConfirmacionAnulacion
          numero={viaje.numero}
          onConfirmar={anular}
          onCancelar={() => setConfirmandoAnulacion(false)}
        />
      )}
    </main>
  )
}
