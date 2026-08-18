import { clasesDeFormulario } from '../../../compartido/ui/clases'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import {
  crearTransportista,
  modificarTransportista,
  obtenerTransportista,
  type TransportistaRequest,
} from './servicioTransportistas'
import { CodigosError } from '../servicios/api'

/** Alta y edición de un transportista (User Story 1, y la edición de la User Story 7). */
export function FormularioTransportista() {
  const { id } = useParams()
  const navegar = useNavigate()
  const editando = id !== undefined
  const transportistaId = Number(id)

  const [nombre, setNombre] = useState('')
  const [cuit, setCuit] = useState('')
  const [tipo, setTipo] = useState<'fisica' | 'juridica' | ''>('')
  const [telefono, setTelefono] = useState('')
  const [email, setEmail] = useState('')

  const [cargando, setCargando] = useState(editando)
  const [guardando, setGuardando] = useState(false)
  const [errorGlobal, setErrorGlobal] = useState<string | null>(null)
  const [erroresDeCampo, setErroresDeCampo] = useState<Record<string, string>>({})

  useEffect(() => {
    if (!editando) return

    let vigente = true

    obtenerTransportista(transportistaId)
      .then((transportista) => {
        if (!vigente) return

        setNombre(transportista.nombre)
        setCuit(transportista.cuit)
        setTipo(transportista.tipo)
        setTelefono(transportista.telefono)
        setEmail(transportista.email)
      })
      .catch(() => {
        if (vigente) setErrorGlobal('No pudimos traer los datos del transportista.')
      })
      .finally(() => {
        if (vigente) setCargando(false)
      })

    return () => {
      vigente = false
    }
  }, [editando, transportistaId])

  async function guardar(evento: React.FormEvent) {
    evento.preventDefault()

    if (tipo === '') {
      setErroresDeCampo({ tipo: 'Elegí el tipo de persona.' })
      return
    }

    setGuardando(true)
    setErrorGlobal(null)
    setErroresDeCampo({})

    const peticion: TransportistaRequest = {
      nombre,
      cuit,
      tipo,
      telefono,
      email,
    }

    try {
      if (editando) {
        await modificarTransportista(transportistaId, peticion)
      } else {
        await crearTransportista(peticion)
      }

      navegar('/transportistas')
    } catch (fallo) {
      if (fallo instanceof ErrorHttp) {
        if (fallo.detalle.codigo === CodigosError.cuitDuplicado) {
          setErrorGlobal(fallo.detalle.mensaje)
          setErroresDeCampo({ cuit: 'Ya registrado.' })
        } else if (fallo.detalle.codigo === CodigosError.datosInvalidos) {
          setErrorGlobal(fallo.detalle.mensaje)
          if (fallo.detalle.campo) {
            setErroresDeCampo({ [fallo.detalle.campo]: 'Valor inválido o requerido.' })
          }
        } else {
          setErrorGlobal(fallo.detalle.mensaje)
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

  const titulo = editando ? 'Editar transportista' : 'Nuevo transportista'

  if (cargando) {
    return (
      <section>
        <EncabezadoDePantalla titulo={titulo} />
        <p role="status">Cargando…</p>
      </section>
    )
  }

  return (
    <section>
      <EncabezadoDePantalla titulo={titulo} />

      <form onSubmit={guardar} noValidate className={clasesDeFormulario}>
        {errorGlobal && <p role="alert">{errorGlobal}</p>}

        <div className={classNameCampo('nombre')}>
          <label htmlFor="nombre">Razón social o nombre completo</label>
          <input
            id="nombre"
            type="text"
            required
            maxLength={100}
            value={nombre}
            onChange={(e) => setNombre(e.target.value)}
            aria-invalid={erroresDeCampo.nombre !== undefined}
          />
          {erroresDeCampo.nombre && <p className="campo__error" role="alert">{erroresDeCampo.nombre}</p>}
        </div>

        <div className={classNameCampo('cuit')}>
          <label htmlFor="cuit">CUIT (con o sin guiones)</label>
          <input
            id="cuit"
            type="text"
            required
            maxLength={20}
            value={cuit}
            onChange={(e) => setCuit(e.target.value)}
            aria-invalid={erroresDeCampo.cuit !== undefined}
          />
          {erroresDeCampo.cuit && <p className="campo__error" role="alert">{erroresDeCampo.cuit}</p>}
        </div>

        <div className={classNameCampo('tipo')}>
          <label htmlFor="tipo">Tipo de persona</label>
          <select
            id="tipo"
            required
            value={tipo}
            onChange={(e) => setTipo(e.target.value as 'fisica' | 'juridica')}
            aria-invalid={erroresDeCampo.tipo !== undefined}
          >
            <option value="" disabled>Seleccioná una opción</option>
            <option value="fisica">Física</option>
            <option value="juridica">Jurídica</option>
          </select>
          {erroresDeCampo.tipo && <p className="campo__error" role="alert">{erroresDeCampo.tipo}</p>}
        </div>

        <div className={classNameCampo('telefono')}>
          <label htmlFor="telefono">Teléfono</label>
          <input
            id="telefono"
            type="tel"
            required
            maxLength={50}
            value={telefono}
            onChange={(e) => setTelefono(e.target.value)}
            aria-invalid={erroresDeCampo.telefono !== undefined}
          />
          {erroresDeCampo.telefono && <p className="campo__error" role="alert">{erroresDeCampo.telefono}</p>}
        </div>

        <div className={classNameCampo('email')}>
          <label htmlFor="email">Email</label>
          <input
            id="email"
            type="email"
            required
            maxLength={254}
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            aria-invalid={erroresDeCampo.email !== undefined}
          />
          {erroresDeCampo.email && <p className="campo__error" role="alert">{erroresDeCampo.email}</p>}
        </div>

        <div className="acciones">
          <button type="submit" disabled={guardando}>
            {editando ? 'Guardar cambios' : 'Guardar transportista'}
          </button>
          <button type="button" onClick={() => navegar('/transportistas')} disabled={guardando}>
            Cancelar
          </button>
        </div>
      </form>
    </section>
  )
}
