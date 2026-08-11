import { useEffect, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { formatearFecha } from '../../../compartido/fechas'
import {
  asignarChoferYVehiculo,
  listarAsignables,
  obtenerViaje,
  type Advertencia,
  type Asignable,
  type Asignables,
  type ViajeDetalle,
} from '../servicios/servicioViajes'

/**
 * El texto de cada opción del desplegable. La unidad observada se ofrece igual —sacarla rompería la
 * carga retroactiva (SC-014)— pero con el motivo al lado: es la palabra, no un color, y explica por
 * qué el módulo de Flota puede estar mostrando esa misma unidad fuera de servicio (convención [003]).
 */
function textoDeLaOpcion(unidad: Asignable) {
  return unidad.observacion === null ? unidad.nombre : `${unidad.nombre} — ${unidad.observacion}`
}

const MENSAJE_SIN_CHOFERES =
  'Todavía no hay choferes activos. Cargá al menos uno en el módulo de Choferes.'
const MENSAJE_SIN_VEHICULOS =
  'Todavía no hay vehículos disponibles. Revisá el módulo de Flota.'

/**
 * Asignación de chofer y vehículo (User Story 3).
 *
 * **Los dos desplegables son obligatorios**: no hay asignación parcial, así que el botón no se
 * habilita con uno solo elegido y un viaje nunca queda con chofer y sin vehículo, ni al revés
 * (FR-019b).
 *
 * **La pantalla dice contra qué fecha se valida.** Toda la evaluación de documentación corre contra
 * la fecha del viaje y no contra hoy, y sin decirlo un rechazo sobre un viaje retroactivo se lee como
 * un error del sistema (FR-024, SC-014).
 *
 * Es la única pantalla del módulo que recibe bloqueos y advertencias por documentación: el bloqueo
 * llega como error y no guarda nada; la advertencia llega **con** el resultado, ya guardado (FR-015a).
 */
export function AsignacionViaje() {
  const { id } = useParams()
  const navegar = useNavigate()
  const viajeId = Number(id)

  const [viaje, setViaje] = useState<ViajeDetalle | null>(null)
  const [asignables, setAsignables] = useState<Asignables | null>(null)
  const [choferId, setChoferId] = useState<number | ''>('')
  const [vehiculoId, setVehiculoId] = useState<number | ''>('')

  const [guardando, setGuardando] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [exito, setExito] = useState<string | null>(null)
  const [advertencias, setAdvertencias] = useState<Advertencia[]>([])

  useEffect(() => {
    let vigente = true

    // En dos pasos y no en paralelo: la lista se pide **con la fecha del viaje**, que es contra la
    // que el servidor evalúa la documentación de cada unidad. Pedirla antes de saber la fecha
    // devolvería observaciones calculadas contra hoy, equivocadas para un viaje retroactivo.
    obtenerViaje(viajeId)
      .then(async (viaje) => ({ viaje, asignables: await listarAsignables(viaje.fecha) }))
      .then(({ viaje, asignables }) => {
        if (!vigente) return

        setViaje(viaje)
        setAsignables(asignables)
        setChoferId(viaje.chofer?.id ?? '')
        setVehiculoId(viaje.vehiculo?.id ?? '')
      })
      .catch(() => {
        if (vigente) setError('No pudimos traer los datos para asignar.')
      })

    return () => {
      vigente = false
    }
  }, [viajeId])

  async function asignar(evento: FormEvent) {
    evento.preventDefault()

    if (choferId === '' || vehiculoId === '') {
      return
    }

    setGuardando(true)
    setError(null)
    setExito(null)
    setAdvertencias([])

    try {
      const respuesta = await asignarChoferYVehiculo(viajeId, choferId, vehiculoId)

      setViaje(respuesta.viaje)
      setAdvertencias(respuesta.advertencias)
      setExito(
        `El viaje ${respuesta.viaje.numero} quedó asignado a ${respuesta.viaje.chofer?.nombre} ` +
          `con ${respuesta.viaje.vehiculo?.nombre}.`,
      )
    } catch (fallo) {
      // Acá caen los dos rechazos propios de esta pantalla: documentación vencida a la fecha del
      // viaje, y unidad ocupada por otro viaje en curso. Los dos traen el mensaje ya escrito.
      setError(
        fallo instanceof ErrorHttp
          ? fallo.detalle.mensaje
          : 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.',
      )
    } finally {
      setGuardando(false)
    }
  }

  if (viaje === null || asignables === null) {
    return (
      <main>
        <h1>Asignar chofer y vehículo</h1>
        {error !== null ? <p role="alert">{error}</p> : <p role="status">Cargando…</p>}
      </main>
    )
  }

  const sinChoferes = asignables.choferes.length === 0
  const sinVehiculos = asignables.vehiculos.length === 0
  const faltaElegir = choferId === '' || vehiculoId === ''

  return (
    <main>
      <h1>Asignar chofer y vehículo — viaje {viaje.numero}</h1>

      <p>La documentación se valida contra la fecha del viaje: {formatearFecha(viaje.fecha)}.</p>

      {error !== null && <p role="alert">{error}</p>}
      {exito !== null && <p role="status">{exito}</p>}

      {advertencias.map((advertencia) => (
        <p key={advertencia.mensaje} role="status">
          {advertencia.mensaje}
        </p>
      ))}

      {sinChoferes && <p role="status">{MENSAJE_SIN_CHOFERES}</p>}
      {sinVehiculos && <p role="status">{MENSAJE_SIN_VEHICULOS}</p>}

      <form onSubmit={asignar} noValidate>
        <div className="campo">
          <label htmlFor="choferId">Chofer</label>
          <select
            id="choferId"
            required
            value={choferId}
            onChange={(evento) => setChoferId(Number(evento.target.value))}
          >
            <option value="" disabled>
              Seleccioná un chofer
            </option>
            {asignables.choferes.map((chofer) => (
              <option key={chofer.id} value={chofer.id}>
                {textoDeLaOpcion(chofer)}
              </option>
            ))}
          </select>
        </div>

        <div className="campo">
          <label htmlFor="vehiculoId">Vehículo</label>
          <select
            id="vehiculoId"
            required
            value={vehiculoId}
            onChange={(evento) => setVehiculoId(Number(evento.target.value))}
          >
            <option value="" disabled>
              Seleccioná un vehículo
            </option>
            {asignables.vehiculos.map((vehiculo) => (
              <option key={vehiculo.id} value={vehiculo.id}>
                {textoDeLaOpcion(vehiculo)}
              </option>
            ))}
          </select>
        </div>

        <div className="acciones">
          {/* Deshabilitado con una sola unidad elegida: no hay asignación parcial (FR-019b). */}
          <button type="submit" disabled={guardando || faltaElegir}>
            Asignar
          </button>
          <button type="button" onClick={() => navegar(`/viajes/${viajeId}`)} disabled={guardando}>
            Volver al viaje
          </button>
        </div>
      </form>
    </main>
  )
}
