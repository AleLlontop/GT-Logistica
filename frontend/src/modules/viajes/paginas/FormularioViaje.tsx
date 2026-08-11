import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { listarClientes, type Cliente } from '../clientes/servicioClientes'
import {
  crearViaje,
  modificarViaje,
  obtenerViaje,
  type ViajePeticion,
} from '../servicios/servicioViajes'

const MENSAJE_SIN_CLIENTES_ACTIVOS =
  'Todavía no hay clientes activos. Cargá al menos un cliente antes de registrar viajes.'

/**
 * Alta y edición de un viaje (User Story 2).
 *
 * **No ofrece chofer ni vehículo**, ni en el alta ni en la edición: el viaje se registra primero y se
 * asigna después, desde su propia pantalla (FR-019a, US3 esc. 14 y 15).
 *
 * **El número se muestra y nunca es editable**, en ningún estado (FR-011, FR-017).
 *
 * Las advertencias que llegan con el resultado —origen igual a destino, carga retroactiva— no frenan
 * nada: el viaje ya quedó guardado, así que viajan a la ficha junto con la confirmación y se anuncian
 * allá con `role="status"` (FR-015a, US2 esc. 10).
 */
export function FormularioViaje() {
  const { id } = useParams()
  const navegar = useNavigate()
  const editando = id !== undefined
  const viajeId = Number(id)

  const [numero, setNumero] = useState<number | null>(null)
  const [clienteId, setClienteId] = useState<number | ''>('')
  const [fecha, setFecha] = useState('')
  const [origen, setOrigen] = useState('')
  const [destino, setDestino] = useState('')
  const [numeroRemito, setNumeroRemito] = useState('')
  const [detalleCarga, setDetalleCarga] = useState('')
  const [importe, setImporte] = useState('0')

  const [clientes, setClientes] = useState<Cliente[] | null>(null)
  const [cargando, setCargando] = useState(editando)
  const [guardando, setGuardando] = useState(false)
  const [errorGlobal, setErrorGlobal] = useState<string | null>(null)
  const [erroresDeCampo, setErroresDeCampo] = useState<Record<string, string>>({})

  // Sólo los activos: un cliente dado de baja deja de ofrecerse al registrar viajes (FR-008).
  useEffect(() => {
    listarClientes({ soloActivos: true, busqueda: '' }, 1)
      .then((pagina) => setClientes(pagina.items))
      .catch(() => setClientes([]))
  }, [])

  useEffect(() => {
    if (!editando) return

    let vigente = true

    obtenerViaje(viajeId)
      .then((viaje) => {
        if (!vigente) return

        setNumero(viaje.numero)
        setClienteId(viaje.cliente.id)
        setFecha(viaje.fecha)
        setOrigen(viaje.origen)
        setDestino(viaje.destino)
        setNumeroRemito(viaje.numeroRemito ?? '')
        setDetalleCarga(viaje.detalleCarga ?? '')
        setImporte(String(viaje.importe))
      })
      .catch(() => {
        if (vigente) setErrorGlobal('No pudimos traer los datos del viaje.')
      })
      .finally(() => {
        if (vigente) setCargando(false)
      })

    return () => {
      vigente = false
    }
  }, [editando, viajeId])

  async function guardar(evento: FormEvent) {
    evento.preventDefault()

    if (clienteId === '') {
      setErroresDeCampo({ clienteId: 'Elegí un cliente.' })
      return
    }

    setGuardando(true)
    setErrorGlobal(null)
    setErroresDeCampo({})

    const peticion: ViajePeticion = {
      clienteId,
      fecha,
      origen,
      destino,
      numeroRemito: numeroRemito.trim() === '' ? null : numeroRemito,
      detalleCarga: detalleCarga.trim() === '' ? null : detalleCarga,
      importe: Number(importe === '' ? 0 : importe),
    }

    try {
      const respuesta = editando
        ? await modificarViaje(viajeId, peticion)
        : await crearViaje(peticion)

      // La confirmación viaja a la ficha y se anuncia allá con `role="status"` (convención [003]).
      // Los textos son los de `contracts/README.md`.
      //
      // Las advertencias viajan **con ella**, no se muestran acá: el guardado ya ocurrió, y un
      // formulario de alta que sigue en pantalla después de guardar invita a apretar *Guardar* de
      // nuevo, que sería un segundo alta —y la que rebota por remito duplicado contra el viaje recién
      // creado—. La advertencia acompaña a la confirmación, sin ningún paso extra (FR-015, FR-015a,
      // US2 esc. 10).
      navegar(`/viajes/${respuesta.viaje.id}`, {
        state: {
          aviso: editando
            ? `Los datos del viaje ${respuesta.viaje.numero} quedaron actualizados.`
            : `El viaje ${respuesta.viaje.numero} quedó registrado como pendiente.`,
          advertencias: respuesta.advertencias,
        },
      })
    } catch (fallo) {
      if (fallo instanceof ErrorHttp) {
        setErrorGlobal(fallo.detalle.mensaje)

        if (fallo.detalle.campo) {
          setErroresDeCampo({ [fallo.detalle.campo]: 'Valor inválido o requerido.' })
        }
      } else {
        setErrorGlobal('Ocurrió un problema inesperado. Volvé a intentar en unos minutos.')
      }
    } finally {
      setGuardando(false)
    }
  }

  function classNameCampo(campo: string) {
    return `campo ${erroresDeCampo[campo] ? 'con-error' : ''}`
  }

  const titulo = editando ? 'Editar viaje' : 'Nuevo viaje'
  const sinClientesActivos = clientes !== null && clientes.length === 0

  if (cargando) {
    return (
      <main>
        <h1>{titulo}</h1>
        <p role="status">Cargando…</p>
      </main>
    )
  }

  return (
    <main>
      <h1>{titulo}</h1>

      {/* El número se muestra y no se edita: lo genera el sistema (FR-011, FR-017). */}
      {numero !== null && <p>Número de viaje: {numero}</p>}

      {errorGlobal && <p role="alert">{errorGlobal}</p>}

      {sinClientesActivos && (
        <div>
          <p role="status">{MENSAJE_SIN_CLIENTES_ACTIVOS}</p>
          <Link to="/clientes/nuevo">Ir a Clientes</Link>
        </div>
      )}

      <form onSubmit={guardar} noValidate>
        <div className={classNameCampo('clienteId')}>
          <label htmlFor="clienteId">Cliente</label>
          <select
            id="clienteId"
            required
            value={clienteId}
            onChange={(evento) => setClienteId(Number(evento.target.value))}
            aria-invalid={erroresDeCampo.clienteId !== undefined}
          >
            <option value="" disabled>
              Seleccioná un cliente
            </option>
            {(clientes ?? []).map((cliente) => (
              <option key={cliente.id} value={cliente.id}>
                {cliente.razonSocial}
              </option>
            ))}
          </select>
          {erroresDeCampo.clienteId && (
            <p className="campo__error" role="alert">
              {erroresDeCampo.clienteId}
            </p>
          )}
        </div>

        <div className={classNameCampo('fecha')}>
          {/* Sin límite de antigüedad ni de anticipación: pasado y futuro son válidos (FR-016). */}
          <label htmlFor="fecha">Fecha del viaje</label>
          <input
            id="fecha"
            type="date"
            required
            value={fecha}
            onChange={(evento) => setFecha(evento.target.value)}
            aria-invalid={erroresDeCampo.fecha !== undefined}
          />
          {erroresDeCampo.fecha && (
            <p className="campo__error" role="alert">
              {erroresDeCampo.fecha}
            </p>
          )}
        </div>

        <div className={classNameCampo('origen')}>
          <label htmlFor="origen">Origen</label>
          <input
            id="origen"
            type="text"
            required
            maxLength={100}
            value={origen}
            onChange={(evento) => setOrigen(evento.target.value)}
            aria-invalid={erroresDeCampo.origen !== undefined}
          />
          {erroresDeCampo.origen && (
            <p className="campo__error" role="alert">
              {erroresDeCampo.origen}
            </p>
          )}
        </div>

        <div className={classNameCampo('destino')}>
          <label htmlFor="destino">Destino</label>
          <input
            id="destino"
            type="text"
            required
            maxLength={100}
            value={destino}
            onChange={(evento) => setDestino(evento.target.value)}
            aria-invalid={erroresDeCampo.destino !== undefined}
          />
          {erroresDeCampo.destino && (
            <p className="campo__error" role="alert">
              {erroresDeCampo.destino}
            </p>
          )}
        </div>

        <div className={classNameCampo('numeroRemito')}>
          <label htmlFor="numeroRemito">Número de remito (opcional)</label>
          <input
            id="numeroRemito"
            type="text"
            maxLength={50}
            value={numeroRemito}
            onChange={(evento) => setNumeroRemito(evento.target.value)}
            aria-invalid={erroresDeCampo.numeroRemito !== undefined}
          />
          {erroresDeCampo.numeroRemito && (
            <p className="campo__error" role="alert">
              {erroresDeCampo.numeroRemito}
            </p>
          )}
        </div>

        <div className={classNameCampo('detalleCarga')}>
          <label htmlFor="detalleCarga">Detalle de la carga (opcional)</label>
          <textarea
            id="detalleCarga"
            maxLength={500}
            value={detalleCarga}
            onChange={(evento) => setDetalleCarga(evento.target.value)}
            aria-invalid={erroresDeCampo.detalleCarga !== undefined}
          />
          {erroresDeCampo.detalleCarga && (
            <p className="campo__error" role="alert">
              {erroresDeCampo.detalleCarga}
            </p>
          )}
        </div>

        <div className={classNameCampo('importe')}>
          {/* El cero es válido: viaje sin cargo o con el importe todavía sin definir (FR-013). */}
          <label htmlFor="importe">Importe en pesos</label>
          <input
            id="importe"
            type="number"
            min={0}
            step="0.01"
            value={importe}
            onChange={(evento) => setImporte(evento.target.value)}
            aria-invalid={erroresDeCampo.importe !== undefined}
          />
          {erroresDeCampo.importe && (
            <p className="campo__error" role="alert">
              {erroresDeCampo.importe}
            </p>
          )}
        </div>

        <div className="acciones">
          <button type="submit" disabled={guardando || sinClientesActivos}>
            {editando ? 'Guardar cambios' : 'Guardar viaje'}
          </button>
          <button type="button" onClick={() => navegar('/viajes')} disabled={guardando}>
            Cancelar
          </button>
        </div>
      </form>
    </main>
  )
}
