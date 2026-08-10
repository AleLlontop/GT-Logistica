import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import {
  listarTransportistas,
  type Transportista,
} from '../../choferes/transportistas/servicioTransportistas'
import { listarTiposVehiculo, type TipoVehiculo } from '../tiposVehiculo/servicioTiposVehiculo'
import { TEXTO_ESTADO_VEHICULO } from '../servicios/estados'
import {
  crearVehiculo,
  modificarVehiculo,
  obtenerVehiculo,
  type VehiculoEstado,
} from '../servicios/servicioFlota'

const MENSAJE_SIN_TIPOS =
  'Todavía no hay ningún tipo de vehículo cargado. Pedile al administrador que cargue al menos uno ' +
  'antes de registrar unidades.'

const MENSAJE_SIN_TRANSPORTISTAS =
  'Todavía no hay ningún transportista cargado. Registrá al menos uno antes de registrar unidades.'

const EXPLICACION_ESTADO_EN_ALTA =
  'Una unidad sin documentación cargada no puede quedar disponible. Cargá su documentación desde la ' +
  'ficha y después cambiá el estado.'

/** Los dos formatos argentinos vigentes, sobre la patente ya normalizada (FR-004). */
const FORMATOS_DE_PATENTE = [/^[A-Z]{3}[0-9]{3}$/, /^[A-Z]{2}[0-9]{3}[A-Z]{2}$/]

/** Misma regla que `NormalizadorPatente` del dominio: mayúsculas y sólo letras y dígitos (FR-003). */
function normalizarPatente(patente: string) {
  return patente.toUpperCase().replace(/[^A-Z0-9]/g, '')
}

/**
 * Alta y edición de una unidad (User Stories 2 y 6).
 *
 * **En el alta, el estado operativo sólo admite "Fuera de servicio"**, y el formulario lo explica de
 * entrada en vez de dejar que el operador lo descubra con un error: una unidad recién registrada no
 * tiene documentos, así que su estado general es `sinDocumentacion` y `disponible` queda rechazado
 * (FR-013, FR-014a, US2 esc. 8).
 *
 * En la edición sí se ofrece "Disponible", y el rechazo de FR-014a llega del servidor nombrando el
 * documento que lo impide.
 */
export function FormularioVehiculo() {
  const { id } = useParams()
  const navegar = useNavigate()

  const editando = id !== undefined
  const vehiculoId = Number(id)

  const [patente, setPatente] = useState('')
  const [marca, setMarca] = useState('')
  const [modelo, setModelo] = useState('')
  const [tipoVehiculoId, setTipoVehiculoId] = useState<number | ''>('')
  const [transportistaId, setTransportistaId] = useState<number | ''>('')
  const [estadoOperativo, setEstadoOperativo] = useState<VehiculoEstado>('fueraDeServicio')

  const [tipos, setTipos] = useState<TipoVehiculo[] | null>(null)
  const [transportistas, setTransportistas] = useState<Transportista[] | null>(null)

  const [errores, setErrores] = useState<Record<string, string>>({})
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null)
  const [guardando, setGuardando] = useState(false)

  // Los selectores ofrecen **sólo lo activo**: un tipo o un transportista dado de baja no se puede
  // elegir (FR-005, FR-008a, FR-011).
  useEffect(() => {
    Promise.all([listarTiposVehiculo(true), listarTransportistas(undefined, true)])
      .then(([losTipos, losTransportistas]) => {
        setTipos(losTipos)
        setTransportistas(losTransportistas)
      })
      .catch(() => {
        setTipos([])
        setTransportistas([])
        setErrorGeneral('No pudimos traer los catálogos. Volvé a intentar en unos minutos.')
      })
  }, [])

  useEffect(() => {
    if (!editando) {
      return
    }

    obtenerVehiculo(vehiculoId)
      .then((vehiculo) => {
        setPatente(vehiculo.patente)
        setMarca(vehiculo.marca)
        setModelo(vehiculo.modelo)
        setTipoVehiculoId(vehiculo.tipo.id)
        setTransportistaId(vehiculo.transportista.id)
        // El **guardado**, no el derivado: si no, editar una unidad parada por papeles vencidos le
        // pisaría en silencio el motivo real a quien opera (FR-014).
        setEstadoOperativo(vehiculo.estadoOperativoGuardado)
      })
      .catch(() => setErrorGeneral('No pudimos traer la unidad. Volvé a intentar en unos minutos.'))
  }, [editando, vehiculoId])

  function validar() {
    const encontrados: Record<string, string> = {}
    const normalizada = normalizarPatente(patente)

    if (!patente.trim()) {
      encontrados.patente = 'Completá la patente.'
    } else if (!FORMATOS_DE_PATENTE.some((formato) => formato.test(normalizada))) {
      // Se marca el campo con el motivo puntual antes de enviar, para no gastar un viaje al servidor
      // en algo que el navegador ya puede ver (FR-004).
      encontrados.patente = 'La patente tiene que tener el formato ABC123 o AB123CD.'
    }

    if (!marca.trim()) encontrados.marca = 'Completá la marca.'
    if (!modelo.trim()) encontrados.modelo = 'Completá el modelo.'
    if (tipoVehiculoId === '') encontrados.tipoVehiculoId = 'Elegí un tipo de vehículo.'
    if (transportistaId === '') encontrados.transportistaId = 'Elegí un transportista.'

    return encontrados
  }

  async function guardar(evento: FormEvent) {
    evento.preventDefault()

    const encontrados = validar()
    setErrores(encontrados)
    setErrorGeneral(null)

    if (Object.keys(encontrados).length > 0) {
      return
    }

    setGuardando(true)

    const peticion = {
      // Se manda normalizada: quien escribe `ab 123 cd` termina con `AB123CD` guardado (FR-003).
      patente: normalizarPatente(patente),
      marca: marca.trim(),
      modelo: modelo.trim(),
      tipoVehiculoId: Number(tipoVehiculoId),
      transportistaId: Number(transportistaId),
      estadoOperativo,
    }

    try {
      if (editando) {
        await modificarVehiculo(vehiculoId, peticion)
        navegar(`/flota/${vehiculoId}`)
      } else {
        const creado = await crearVehiculo(peticion)
        navegar(`/flota/${creado.id}`)
      }
    } catch (fallo) {
      // Nada se limpia: lo tipeado tiene que seguir en pantalla para poder corregir y reintentar.
      if (fallo instanceof ErrorHttp) {
        if (fallo.detalle.campo !== undefined) {
          setErrores({ [fallo.detalle.campo]: fallo.detalle.mensaje })
        } else {
          setErrorGeneral(fallo.detalle.mensaje)
        }
      } else {
        setErrorGeneral('Ocurrió un problema inesperado. Volvé a intentar en unos minutos.')
      }
    } finally {
      setGuardando(false)
    }
  }

  if (tipos === null || transportistas === null) {
    return (
      <main>
        <p role="status">Cargando catálogos…</p>
      </main>
    )
  }

  // US2 esc. 6 y 7: sin tipos activos o sin transportistas no se puede registrar nada, y se dice por
  // qué con el enlace que lo resuelve, en vez de mostrar un formulario que va a fallar.
  if (!editando && tipos.length === 0) {
    return (
      <main>
        <h1>Registrar unidad</h1>
        <p role="alert">{MENSAJE_SIN_TIPOS}</p>
        <Link to="/tipos-vehiculo">Ir a Tipos de vehículo</Link>
      </main>
    )
  }

  if (!editando && transportistas.length === 0) {
    return (
      <main>
        <h1>Registrar unidad</h1>
        <p role="alert">{MENSAJE_SIN_TRANSPORTISTAS}</p>
        <Link to="/transportistas">Ir a Transportistas</Link>
      </main>
    )
  }

  return (
    <main>
      <h1>{editando ? 'Editar unidad' : 'Registrar unidad'}</h1>

      {errorGeneral !== null && <p role="alert">{errorGeneral}</p>}

      <form onSubmit={guardar} noValidate>
        <div className="campo">
          <label htmlFor="patente">Patente</label>
          <input
            id="patente"
            type="text"
            maxLength={10}
            value={patente}
            onChange={(evento) => setPatente(evento.target.value)}
            required
            aria-invalid={errores.patente !== undefined}
            aria-describedby="ayuda-patente"
          />
          <small id="ayuda-patente">Formato ABC123 o AB123CD. Los espacios y guiones se ignoran.</small>
          {errores.patente !== undefined && (
            <p className="campo__error" role="alert">
              {errores.patente}
            </p>
          )}
        </div>

        <div className="campo">
          <label htmlFor="marca">Marca</label>
          <input
            id="marca"
            type="text"
            maxLength={50}
            value={marca}
            onChange={(evento) => setMarca(evento.target.value)}
            required
            aria-invalid={errores.marca !== undefined}
          />
          {errores.marca !== undefined && (
            <p className="campo__error" role="alert">
              {errores.marca}
            </p>
          )}
        </div>

        <div className="campo">
          <label htmlFor="modelo">Modelo</label>
          <input
            id="modelo"
            type="text"
            maxLength={50}
            value={modelo}
            onChange={(evento) => setModelo(evento.target.value)}
            required
            aria-invalid={errores.modelo !== undefined}
          />
          {errores.modelo !== undefined && (
            <p className="campo__error" role="alert">
              {errores.modelo}
            </p>
          )}
        </div>

        <div className="campo">
          <label htmlFor="tipoVehiculoId">Tipo de vehículo</label>
          <select
            id="tipoVehiculoId"
            value={tipoVehiculoId}
            onChange={(evento) =>
              setTipoVehiculoId(evento.target.value === '' ? '' : Number(evento.target.value))
            }
            required
            aria-invalid={errores.tipoVehiculoId !== undefined}
          >
            <option value="">Elegí un tipo</option>
            {tipos.map((tipo) => (
              <option key={tipo.id} value={tipo.id}>
                {tipo.nombre}
              </option>
            ))}
          </select>
          {errores.tipoVehiculoId !== undefined && (
            <p className="campo__error" role="alert">
              {errores.tipoVehiculoId}
            </p>
          )}
        </div>

        <div className="campo">
          <label htmlFor="transportistaId">Transportista</label>
          <select
            id="transportistaId"
            value={transportistaId}
            onChange={(evento) =>
              setTransportistaId(evento.target.value === '' ? '' : Number(evento.target.value))
            }
            required
            aria-invalid={errores.transportistaId !== undefined}
          >
            <option value="">Elegí un transportista</option>
            {transportistas.map((transportista) => (
              <option key={transportista.id} value={transportista.id}>
                {transportista.nombre}
              </option>
            ))}
          </select>
          {errores.transportistaId !== undefined && (
            <p className="campo__error" role="alert">
              {errores.transportistaId}
            </p>
          )}
        </div>

        <div className="campo">
          <label htmlFor="estadoOperativo">Estado operativo</label>
          <select
            id="estadoOperativo"
            value={estadoOperativo}
            onChange={(evento) => setEstadoOperativo(evento.target.value as VehiculoEstado)}
            required
            aria-invalid={errores.estadoOperativo !== undefined}
            aria-describedby={editando ? undefined : 'ayuda-estado'}
          >
            {/* En el alta no se ofrece "Disponible": la unidad todavía no tiene documentación
                (FR-013, FR-014a, US2 esc. 8). */}
            {editando && <option value="disponible">{TEXTO_ESTADO_VEHICULO.disponible}</option>}
            <option value="fueraDeServicio">{TEXTO_ESTADO_VEHICULO.fueraDeServicio}</option>
          </select>

          {!editando && <small id="ayuda-estado">{EXPLICACION_ESTADO_EN_ALTA}</small>}

          {errores.estadoOperativo !== undefined && (
            <p className="campo__error" role="alert">
              {errores.estadoOperativo}
            </p>
          )}
        </div>

        <div className="acciones">
          <button type="submit" disabled={guardando}>
            {guardando ? 'Guardando…' : editando ? 'Guardar cambios' : 'Registrar unidad'}
          </button>
          <button
            type="button"
            onClick={() => navegar(editando ? `/flota/${vehiculoId}` : '/flota')}
            disabled={guardando}
          >
            Cancelar
          </button>
        </div>
      </form>
    </main>
  )
}
