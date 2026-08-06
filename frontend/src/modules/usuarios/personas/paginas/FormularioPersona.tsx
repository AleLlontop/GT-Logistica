import { useEffect, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../../compartido/clienteHttp'
import {
  crearPersona,
  modificarPersona,
  obtenerPersona,
  TIPOS_DE_PERSONA,
  type DatosPersona,
} from '../servicios/personas'

const CAMPOS: readonly (keyof DatosPersona)[] = [
  'nombre',
  'apellido',
  'dni',
  'tipo',
  'telefono',
  'email',
  'fechaNacimiento',
]

const VACIO: DatosPersona = {
  nombre: '',
  apellido: '',
  dni: '',
  tipo: 'chofer',
  telefono: '',
  email: '',
  fechaNacimiento: '',
}

type ErroresDeCampo = Partial<Record<keyof DatosPersona, string>>

/**
 * Alta y edición de una persona (User Story 6).
 *
 * Son exactamente los siete datos de FR-026 y ninguno más: la lista es taxativa y no se amplía sin
 * cambiar antes la spec.
 */
export function FormularioPersona() {
  const navegar = useNavigate()
  const { id } = useParams<{ id: string }>()

  const esEdicion = id !== undefined
  const idPersona = esEdicion ? Number(id) : null

  const [datos, setDatos] = useState<DatosPersona>(VACIO)
  const [cargando, setCargando] = useState(esEdicion)
  const [errores, setErrores] = useState<ErroresDeCampo>({})
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  useEffect(() => {
    if (idPersona === null) {
      return
    }

    let vigente = true

    obtenerPersona(idPersona)
      .then((persona) => {
        if (vigente) {
          setDatos({
            nombre: persona.nombre,
            apellido: persona.apellido,
            dni: persona.dni,
            tipo: persona.tipo,
            telefono: persona.telefono,
            email: persona.email,
            fechaNacimiento: persona.fechaNacimiento,
          })
        }
      })
      .catch(() => {
        if (vigente) {
          setErrorGeneral('No pudimos traer los datos de esa persona.')
        }
      })
      .finally(() => {
        if (vigente) {
          setCargando(false)
        }
      })

    return () => {
      vigente = false
    }
  }, [idPersona])

  function actualizar<C extends keyof DatosPersona>(campo: C, valor: DatosPersona[C]) {
    setDatos((actuales) => ({ ...actuales, [campo]: valor }))
  }

  function validarEnPantalla(): ErroresDeCampo {
    const encontrados: ErroresDeCampo = {}

    if (datos.nombre.trim() === '') {
      encontrados.nombre = 'Escribí el nombre.'
    }

    if (datos.apellido.trim() === '') {
      encontrados.apellido = 'Escribí el apellido.'
    }

    // Sólo dígitos: con puntos o letras, el mismo DNI se guardaría distinto según quién lo escriba
    // y la unicidad dejaría de detectar duplicados reales (FR-027).
    if (!/^\d{7,15}$/.test(datos.dni.trim())) {
      encontrados.dni = 'El DNI tiene que ser sólo números, sin puntos.'
    }

    if (datos.telefono.trim() === '') {
      encontrados.telefono = 'Escribí un teléfono.'
    }

    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(datos.email.trim())) {
      encontrados.email = 'Escribí un email válido, con formato nombre@dominio.'
    }

    if (datos.fechaNacimiento === '') {
      encontrados.fechaNacimiento = 'Elegí la fecha de nacimiento.'
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
      if (idPersona === null) {
        await crearPersona(datos)
      } else {
        await modificarPersona(idPersona, datos)
      }

      navegar('/personas', { replace: true })
    } catch (fallo) {
      if (fallo instanceof ErrorHttp) {
        const campo = fallo.detalle.campo as keyof DatosPersona | undefined

        if (campo !== undefined && CAMPOS.includes(campo)) {
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

  if (cargando) {
    return (
      <main>
        <p role="status">Cargando persona…</p>
      </main>
    )
  }

  return (
    <main>
      <h1>{esEdicion ? 'Editar persona' : 'Nueva persona'}</h1>

      <form onSubmit={alEnviar} noValidate>
        {errorGeneral !== null && (
          <p className="formulario__error" role="alert">
            {errorGeneral}
          </p>
        )}

        <CampoTexto
          id="nombre"
          etiqueta="Nombre"
          valor={datos.nombre}
          error={errores.nombre}
          onCambio={(valor) => actualizar('nombre', valor)}
        />

        <CampoTexto
          id="apellido"
          etiqueta="Apellido"
          valor={datos.apellido}
          error={errores.apellido}
          onCambio={(valor) => actualizar('apellido', valor)}
        />

        <CampoTexto
          id="dni"
          etiqueta="DNI"
          valor={datos.dni}
          error={errores.dni}
          onCambio={(valor) => actualizar('dni', valor)}
        />

        <div className="campo">
          <label htmlFor="tipo">Tipo</label>
          <select
            id="tipo"
            name="tipo"
            value={datos.tipo}
            onChange={(evento) => actualizar('tipo', evento.target.value as DatosPersona['tipo'])}
          >
            {TIPOS_DE_PERSONA.map((tipo) => (
              <option key={tipo.codigo} value={tipo.codigo}>
                {tipo.nombre}
              </option>
            ))}
          </select>
        </div>

        <CampoTexto
          id="telefono"
          etiqueta="Teléfono"
          valor={datos.telefono}
          error={errores.telefono}
          onCambio={(valor) => actualizar('telefono', valor)}
        />

        <CampoTexto
          id="email"
          etiqueta="Email"
          tipo="email"
          valor={datos.email}
          error={errores.email}
          onCambio={(valor) => actualizar('email', valor)}
        />

        <CampoTexto
          id="fechaNacimiento"
          etiqueta="Fecha de nacimiento"
          tipo="date"
          valor={datos.fechaNacimiento}
          error={errores.fechaNacimiento}
          onCambio={(valor) => actualizar('fechaNacimiento', valor)}
        />

        <button type="submit" disabled={enviando}>
          {enviando ? 'Guardando…' : 'Guardar'}
        </button>
      </form>
    </main>
  )
}

interface CampoProps {
  id: string
  etiqueta: string
  valor: string
  error?: string
  tipo?: 'text' | 'email' | 'date'
  onCambio: (valor: string) => void
}

/** Campo con etiqueta asociada y su error anunciado a lectores de pantalla. */
function CampoTexto({ id, etiqueta, valor, error, tipo = 'text', onCambio }: CampoProps) {
  return (
    <div className="campo">
      <label htmlFor={id}>{etiqueta}</label>
      <input
        id={id}
        name={id}
        type={tipo}
        value={valor}
        onChange={(evento) => onCambio(evento.target.value)}
        required
        aria-required="true"
        aria-invalid={error !== undefined}
        aria-describedby={error !== undefined ? `error-${id}` : undefined}
      />
      {error !== undefined && (
        <p className="campo__error" id={`error-${id}`} role="alert">
          {error}
        </p>
      )}
    </div>
  )
}
