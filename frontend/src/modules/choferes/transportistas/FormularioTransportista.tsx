import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { crearTransportista, type TransportistaRequest } from './servicioTransportistas'
import { CodigosError } from '../servicios/api'

export function FormularioTransportista() {
  const navegar = useNavigate()
  
  const [nombre, setNombre] = useState('')
  const [cuit, setCuit] = useState('')
  const [tipo, setTipo] = useState<'fisica' | 'juridica' | ''>('')
  const [telefono, setTelefono] = useState('')
  const [email, setEmail] = useState('')
  
  const [guardando, setGuardando] = useState(false)
  const [errorGlobal, setErrorGlobal] = useState<string | null>(null)
  const [erroresDeCampo, setErroresDeCampo] = useState<Record<string, string>>({})

  async function guardar(evento: React.FormEvent) {
    evento.preventDefault()
    
    if (tipo === '') {
      setErroresDeCampo({ tipo: 'Requerido.' })
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
      await crearTransportista(peticion)
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

  return (
    <main>
      <h1>Nuevo transportista</h1>

      <form onSubmit={guardar} noValidate>
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
            Guardar transportista
          </button>
          <button type="button" onClick={() => navegar('/transportistas')} disabled={guardando}>
            Cancelar
          </button>
        </div>
      </form>
    </main>
  )
}
