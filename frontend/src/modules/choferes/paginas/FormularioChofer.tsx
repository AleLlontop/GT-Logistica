import { useEffect, useState, type FormEvent } from 'react'
import { Link } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { crearChofer, type ChoferDetalle } from '../servicios/servicioChoferes'
import { listarTransportistas, type Transportista } from '../transportistas/servicioTransportistas'

/** Errores de formato por campo. */
type ErroresDeCampo = Partial<
  Record<
    | 'nombre'
    | 'apellido'
    | 'dni'
    | 'cuil'
    | 'fechaNacimiento'
    | 'telefono'
    | 'email'
    | 'transportistaId',
    string
  >
>

const CAMPOS_DEL_FORMULARIO: readonly string[] = [
  'nombre',
  'apellido',
  'dni',
  'cuil',
  'fechaNacimiento',
  'telefono',
  'email',
  'transportistaId',
]

// Texto exacto de `contracts/README.md` para el selector sin transportistas activos.
const MENSAJE_SIN_TRANSPORTISTAS_ACTIVOS =
  'No hay transportistas activos. Registrá uno desde la pantalla Transportistas.'

const VACIO = {
  nombre: '',
  apellido: '',
  dni: '',
  cuil: '',
  fechaNacimiento: '',
  telefono: '',
  email: '',
}

/**
 * Alta de un chofer (User Story 2).
 *
 * El aviso de reutilización de persona lo da el backend **al guardar**, no una búsqueda previa: el
 * padrón de personas es del Módulo 2 y su endpoint exige `usuarios.gestionar`, que un usuario de
 * Tráfico no tiene (FR-027). Consultarlo desde acá funcionaría para el administrador y fallaría en
 * silencio justo para el rol que usa esta pantalla todos los días.
 */
export function FormularioChofer() {
  const [datos, setDatos] = useState(VACIO)
  const [transportistaId, setTransportistaId] = useState<number | ''>('')

  const [transportistas, setTransportistas] = useState<Transportista[] | null>(null)

  const [errores, setErrores] = useState<ErroresDeCampo>({})
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)
  const [registrado, setRegistrado] = useState<ChoferDetalle | null>(null)

  useEffect(() => {
    let vigente = true

    listarTransportistas('', true)
      .then((resultado) => {
        if (!vigente) return

        setTransportistas(resultado)
        if (resultado.length > 0) {
          setTransportistaId(resultado[0].id)
        }
      })
      .catch(() => {
        if (!vigente) return

        setTransportistas([])
        setErrorGeneral('No pudimos traer los transportistas. Volvé a intentar en unos minutos.')
      })

    return () => {
      vigente = false
    }
  }, [])

  function actualizar<C extends keyof typeof VACIO>(campo: C, valor: string) {
    setDatos((previos) => ({ ...previos, [campo]: valor }))
  }

  function validarEnPantalla(): ErroresDeCampo {
    const encontrados: ErroresDeCampo = {}

    if (!datos.nombre.trim()) encontrados.nombre = 'Completá el nombre.'
    if (!datos.apellido.trim()) encontrados.apellido = 'Completá el apellido.'
    if (!datos.dni.trim()) encontrados.dni = 'Completá el DNI.'
    if (!datos.cuil.trim()) encontrados.cuil = 'Completá el CUIL.'
    if (!datos.fechaNacimiento) encontrados.fechaNacimiento = 'Completá la fecha de nacimiento.'
    if (!datos.telefono.trim()) encontrados.telefono = 'Completá el teléfono.'

    if (!datos.email.trim()) {
      encontrados.email = 'Completá el email.'
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(datos.email.trim())) {
      encontrados.email = 'Revisá el email.'
    }

    if (transportistaId === '') {
      encontrados.transportistaId = 'Elegí un transportista.'
    }

    return encontrados
  }

  async function alEnviar(evento: FormEvent) {
    evento.preventDefault()

    const deFormato = validarEnPantalla()
    setErrores(deFormato)
    setErrorGeneral(null)

    if (Object.keys(deFormato).length > 0) {
      return
    }

    setEnviando(true)

    try {
      const creado = await crearChofer({
        nombre: datos.nombre.trim(),
        apellido: datos.apellido.trim(),
        dni: datos.dni.trim(),
        cuil: datos.cuil.trim(),
        fechaNacimiento: datos.fechaNacimiento,
        telefono: datos.telefono.trim(),
        email: datos.email.trim(),
        transportistaId: Number(transportistaId),
      })

      setRegistrado(creado)
      setDatos(VACIO)
    } catch (fallo) {
      if (fallo instanceof ErrorHttp) {
        const campo = fallo.detalle.campo

        if (campo !== undefined && CAMPOS_DEL_FORMULARIO.includes(campo)) {
          setErrores({ [campo]: fallo.detalle.mensaje })
        } else {
          setErrorGeneral(fallo.detalle.mensaje)
        }
      } else {
        setErrorGeneral('Ocurrió un problema inesperado. Volvé a intentar en unos minutos.')
      }
    } finally {
      setEnviando(false)
    }
  }

  if (transportistas === null) {
    return (
      <main>
        <h1>Nuevo chofer</h1>
        <p role="status">Cargando transportistas…</p>
      </main>
    )
  }

  // US2 esc. 4: sin transportistas activos el alta no se puede completar, y se dice por qué con el
  // enlace a la pantalla que lo resuelve.
  if (transportistas.length === 0) {
    return (
      <main>
        <h1>Nuevo chofer</h1>
        <p role="alert">{MENSAJE_SIN_TRANSPORTISTAS_ACTIVOS}</p>
        <Link to="/transportistas/nuevo">Registrar un transportista</Link>
      </main>
    )
  }

  return (
    <main>
      <h1>Nuevo chofer</h1>

      {registrado !== null && (
        <div role="status">
          <p>
            {registrado.reutilizoPersona
              ? `El chofer ${registrado.apellido}, ${registrado.nombre} se registró correctamente, reutilizando la persona que ya estaba en el padrón.`
              : `El chofer ${registrado.apellido}, ${registrado.nombre} se registró correctamente.`}
          </p>
          <Link to="/choferes">Volver al listado de choferes</Link>
        </div>
      )}

      <form onSubmit={alEnviar} noValidate>
        {errorGeneral !== null && (
          <p className="formulario__error" role="alert">
            {errorGeneral}
          </p>
        )}

        <div className="campo">
          <label htmlFor="dni">DNI</label>
          <input
            id="dni"
            name="dni"
            type="text"
            value={datos.dni}
            onChange={(evento) => actualizar('dni', evento.target.value)}
            autoComplete="off"
            required
            aria-invalid={errores.dni !== undefined}
            aria-describedby={errores.dni !== undefined ? 'error-dni' : undefined}
          />
          {errores.dni !== undefined && (
            <p className="campo__error" id="error-dni" role="alert">
              {errores.dni}
            </p>
          )}
        </div>

        <div className="campo">
          <label htmlFor="nombre">Nombre</label>
          <input
            id="nombre"
            name="nombre"
            type="text"
            value={datos.nombre}
            onChange={(evento) => actualizar('nombre', evento.target.value)}
            required
            aria-invalid={errores.nombre !== undefined}
            aria-describedby={errores.nombre !== undefined ? 'error-nombre' : undefined}
          />
          {errores.nombre !== undefined && (
            <p className="campo__error" id="error-nombre" role="alert">
              {errores.nombre}
            </p>
          )}
        </div>

        <div className="campo">
          <label htmlFor="apellido">Apellido</label>
          <input
            id="apellido"
            name="apellido"
            type="text"
            value={datos.apellido}
            onChange={(evento) => actualizar('apellido', evento.target.value)}
            required
            aria-invalid={errores.apellido !== undefined}
            aria-describedby={errores.apellido !== undefined ? 'error-apellido' : undefined}
          />
          {errores.apellido !== undefined && (
            <p className="campo__error" id="error-apellido" role="alert">
              {errores.apellido}
            </p>
          )}
        </div>

        <div className="campo">
          <label htmlFor="fechaNacimiento">Fecha de nacimiento</label>
          <input
            id="fechaNacimiento"
            name="fechaNacimiento"
            type="date"
            value={datos.fechaNacimiento}
            onChange={(evento) => actualizar('fechaNacimiento', evento.target.value)}
            required
            aria-invalid={errores.fechaNacimiento !== undefined}
            aria-describedby={
              errores.fechaNacimiento !== undefined ? 'error-fechaNacimiento' : undefined
            }
          />
          {errores.fechaNacimiento !== undefined && (
            <p className="campo__error" id="error-fechaNacimiento" role="alert">
              {errores.fechaNacimiento}
            </p>
          )}
        </div>

        <div className="campo">
          <label htmlFor="cuil">CUIL</label>
          <input
            id="cuil"
            name="cuil"
            type="text"
            value={datos.cuil}
            onChange={(evento) => actualizar('cuil', evento.target.value)}
            required
            aria-invalid={errores.cuil !== undefined}
            aria-describedby={errores.cuil !== undefined ? 'error-cuil' : undefined}
          />
          {errores.cuil !== undefined && (
            <p className="campo__error" id="error-cuil" role="alert">
              {errores.cuil}
            </p>
          )}
        </div>

        <div className="campo">
          <label htmlFor="telefono">Teléfono</label>
          <input
            id="telefono"
            name="telefono"
            type="text"
            value={datos.telefono}
            onChange={(evento) => actualizar('telefono', evento.target.value)}
            required
            aria-invalid={errores.telefono !== undefined}
            aria-describedby={errores.telefono !== undefined ? 'error-telefono' : undefined}
          />
          {errores.telefono !== undefined && (
            <p className="campo__error" id="error-telefono" role="alert">
              {errores.telefono}
            </p>
          )}
        </div>

        <div className="campo">
          <label htmlFor="email">Email</label>
          <input
            id="email"
            name="email"
            type="email"
            value={datos.email}
            onChange={(evento) => actualizar('email', evento.target.value)}
            required
            aria-invalid={errores.email !== undefined}
            aria-describedby={errores.email !== undefined ? 'error-email' : undefined}
          />
          {errores.email !== undefined && (
            <p className="campo__error" id="error-email" role="alert">
              {errores.email}
            </p>
          )}
        </div>

        <div className="campo">
          <label htmlFor="transportistaId">Transportista</label>
          <select
            id="transportistaId"
            name="transportistaId"
            value={transportistaId}
            onChange={(evento) =>
              setTransportistaId(evento.target.value === '' ? '' : Number(evento.target.value))
            }
            required
            aria-invalid={errores.transportistaId !== undefined}
            aria-describedby={
              errores.transportistaId !== undefined ? 'error-transportistaId' : undefined
            }
          >
            {transportistas.map((transportista) => (
              <option key={transportista.id} value={transportista.id}>
                {transportista.nombre}
              </option>
            ))}
          </select>
          {errores.transportistaId !== undefined && (
            <p className="campo__error" id="error-transportistaId" role="alert">
              {errores.transportistaId}
            </p>
          )}
        </div>

        <button type="submit" disabled={enviando}>
          {enviando ? 'Guardando…' : 'Guardar chofer'}
        </button>
      </form>
    </main>
  )
}
