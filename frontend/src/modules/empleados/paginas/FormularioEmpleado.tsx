import { useEffect, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import type { Persona } from '../../../compartido/tipos'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { BarraDeAcciones, LEYENDA_DE_OBLIGATORIOS } from '../../../compartido/ui/BarraDeAcciones'
import { Boton } from '../../../compartido/ui/Boton'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import { IconoEnRegla } from '../../../compartido/ui/iconos'
import { clasesDeAvisoDePantalla, clasesDeFormularioSimple } from '../../../compartido/ui/clases'
import {
  crearEmpleado,
  modificarEmpleado,
  obtenerEmpleado,
  type DatosEmpleado,
} from '../servicios/servicioEmpleados'
import { errorDeNombre, errorDeTelefono } from '../../usuarios/personas/servicios/validacion'

const VACIO: DatosEmpleado = {
  nombre: '',
  apellido: '',
  dni: '',
  telefono: '',
  email: '',
  fechaNacimiento: '',
}

type Campo = keyof DatosEmpleado
type ErroresDeCampo = Partial<Record<Campo, string>>

interface Props {
  puedeGestionar: boolean
}

export function FormularioEmpleado({ puedeGestionar }: Props) {
  const { id } = useParams<{ id: string }>()
  const navegar = useNavigate()
  const esEdicion = id !== undefined
  const empleadoId = Number(id)

  const [datos, setDatos] = useState<DatosEmpleado>(VACIO)
  const [cargando, setCargando] = useState(esEdicion)
  const [cargaFallida, setCargaFallida] = useState(false)
  const [guardando, setGuardando] = useState(false)
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null)
  const [errores, setErrores] = useState<ErroresDeCampo>({})

  useEffect(() => {
    if (!puedeGestionar || !esEdicion) {
      setCargando(false)
      return
    }

    if (!Number.isSafeInteger(empleadoId) || empleadoId <= 0) {
      setCargaFallida(true)
      setErrorGeneral('No encontramos ese empleado.')
      setCargando(false)
      return
    }

    let vigente = true

    obtenerEmpleado(empleadoId)
      .then((persona: Persona) => {
        if (!vigente) return

        if (persona.tipo !== 'empleado') {
          setCargaFallida(true)
          setErrorGeneral('El registro no corresponde a un empleado.')
          return
        }

        setDatos({
          nombre: persona.nombre,
          apellido: persona.apellido,
          dni: persona.dni,
          telefono: persona.telefono,
          email: persona.email,
          fechaNacimiento: persona.fechaNacimiento,
        })
      })
      .catch((fallo: unknown) => {
        if (!vigente) return

        setCargaFallida(true)
        setErrorGeneral(
          fallo instanceof ErrorHttp && fallo.estado === 404
            ? 'No encontramos ese empleado.'
            : fallo instanceof ErrorHttp && fallo.estado === 403
              ? 'No tenés permiso para consultar este empleado.'
              : 'No pudimos cargar los datos del empleado.',
        )
      })
      .finally(() => {
        if (vigente) setCargando(false)
      })

    return () => {
      vigente = false
    }
  }, [puedeGestionar, esEdicion, empleadoId])

  function actualizar(campo: Campo, valor: string) {
    setDatos((actuales) => ({ ...actuales, [campo]: valor }))
    setErrores((actuales) => ({ ...actuales, [campo]: undefined }))
    setErrorGeneral(null)
  }

  function mostrarErrorDeCampo(campo: Campo) {
    const mensaje = errores[campo]
    return mensaje ? (
      <p className="campo__error" id={`error-${campo}`} role="alert">
        {mensaje}
      </p>
    ) : null
  }

  function asignarErrorDeServidor(fallo: unknown) {
    if (fallo instanceof ErrorHttp) {
      const campo = fallo.detalle.campo
      setErrorGeneral(fallo.detalle.mensaje)

      if (campo && Object.prototype.hasOwnProperty.call(VACIO, campo)) {
        setErrores({ [campo as Campo]: fallo.detalle.mensaje })
      }
      return
    }

    setErrorGeneral(fallo instanceof Error ? fallo.message : 'No pudimos guardar al empleado.')
  }

  async function guardar(evento: FormEvent<HTMLFormElement>) {
    evento.preventDefault()
    if (!puedeGestionar || guardando) return

    setErrores({})
    setErrorGeneral(null)

    const encontrados: ErroresDeCampo = {}
    // FR-026a del Módulo 2: la misma regla que el formulario de Personas y que el servidor.
    const errorEnNombre = datos.nombre.trim()
      ? errorDeNombre(datos.nombre, 'nombre')
      : 'Completá el nombre.'
    if (errorEnNombre !== undefined) {
      encontrados.nombre = errorEnNombre
    }
    const errorEnApellido = datos.apellido.trim()
      ? errorDeNombre(datos.apellido, 'apellido')
      : 'Completá el apellido.'
    if (errorEnApellido !== undefined) {
      encontrados.apellido = errorEnApellido
    }
    if (!/^\d{7,15}$/.test(datos.dni.trim())) {
      encontrados.dni = 'Ingresá un DNI de 7 a 15 dígitos, sin puntos ni espacios.'
    }
    const errorEnTelefono = datos.telefono.trim()
      ? errorDeTelefono(datos.telefono)
      : 'Completá el teléfono.'
    if (errorEnTelefono !== undefined) {
      encontrados.telefono = errorEnTelefono
    }
    if (!datos.email.trim()) {
      encontrados.email = 'Completá el email.'
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(datos.email.trim())) {
      encontrados.email = 'Ingresá un email válido.'
    }
    if (!datos.fechaNacimiento) {
      encontrados.fechaNacimiento = 'Completá la fecha de nacimiento.'
    }

    if (Object.keys(encontrados).length > 0) {
      setErrores(encontrados)
      setErrorGeneral('Revisá los campos marcados antes de guardar.')
      return
    }

    const peticion: DatosEmpleado = {
      nombre: datos.nombre.trim(),
      apellido: datos.apellido.trim(),
      dni: datos.dni.trim(),
      telefono: datos.telefono.trim(),
      email: datos.email.trim(),
      fechaNacimiento: datos.fechaNacimiento,
    }

    setGuardando(true)
    try {
      if (esEdicion) await modificarEmpleado(empleadoId, peticion)
      else await crearEmpleado(peticion)
      navegar('/empleados', { replace: true })
    } catch (fallo) {
      asignarErrorDeServidor(fallo)
    } finally {
      setGuardando(false)
    }
  }

  if (!puedeGestionar) {
    return (
      <section>
        <EncabezadoDePantalla
          titulo={esEdicion ? 'Modificar empleado' : 'Registrar empleado'}
          volverA={{ ruta: '/empleados', etiqueta: 'Volver a empleados' }}
        />
        <p role="alert" className={clasesDeAvisoDePantalla.advertencia}>
          No tenés permiso para registrar ni modificar empleados.
        </p>
      </section>
    )
  }

  const titulo = esEdicion ? 'Modificar empleado' : 'Registrar empleado'

  if (cargando) {
    return (
      <section>
        <EncabezadoDePantalla titulo={titulo} volverA={{ ruta: '/empleados', etiqueta: 'Volver a empleados' }} />
        <EstadoVacio caso="cargando">Cargando empleado…</EstadoVacio>
      </section>
    )
  }

  if (cargaFallida) {
    return (
      <section>
        <EncabezadoDePantalla titulo={titulo} volverA={{ ruta: '/empleados', etiqueta: 'Volver a empleados' }} />
        <p role="alert" className={clasesDeAvisoDePantalla.error}>
          {errorGeneral ?? 'No pudimos cargar los datos del empleado.'}
        </p>
      </section>
    )
  }

  return (
    <section>
      <EncabezadoDePantalla
        titulo={titulo}
        volverA={{ ruta: '/empleados', etiqueta: 'Volver a empleados' }}
      />

      {errorGeneral !== null && (
        <p role="alert" className={`${clasesDeAvisoDePantalla.error} mb-4`}>
          {errorGeneral}
        </p>
      )}

      <form className={clasesDeFormularioSimple} onSubmit={guardar} noValidate>
        <div className="grid gap-x-5 gap-y-[18px] md:grid-cols-2">
          <div className={`campo ${errores.nombre ? 'con-error' : ''}`}>
            <label htmlFor="nombre">Nombre</label>
            <input
              id="nombre"
              name="nombre"
              type="text"
              autoComplete="given-name"
              value={datos.nombre}
              onChange={(e) => actualizar('nombre', e.target.value)}
              maxLength={100}
              required
              aria-invalid={errores.nombre !== undefined}
              aria-describedby={errores.nombre ? 'error-nombre' : undefined}
              pattern="[A-Za-zÁÉÍÓÚÜÑáéíóúüñ '’.-]+"
              title="Usá letras, espacios, guiones, apóstrofes o puntos."
            />
            {mostrarErrorDeCampo('nombre')}
          </div>

          <div className={`campo ${errores.apellido ? 'con-error' : ''}`}>
            <label htmlFor="apellido">Apellido</label>
            <input
              id="apellido"
              name="apellido"
              type="text"
              autoComplete="family-name"
              value={datos.apellido}
              onChange={(e) => actualizar('apellido', e.target.value)}
              maxLength={100}
              required
              aria-invalid={errores.apellido !== undefined}
              aria-describedby={errores.apellido ? 'error-apellido' : undefined}
              pattern="[A-Za-zÁÉÍÓÚÜÑáéíóúüñ '’.-]+"
              title="Usá letras, espacios, guiones, apóstrofes o puntos."
            />
            {mostrarErrorDeCampo('apellido')}
          </div>

          <div className={`campo ${errores.dni ? 'con-error' : ''}`}>
            <label htmlFor="dni">DNI</label>
            <input
              id="dni"
              name="dni"
              inputMode="numeric"
              autoComplete="off"
              value={datos.dni}
              onChange={(e) => actualizar('dni', e.target.value)}
              minLength={7}
              maxLength={15}
              pattern="[0-9]+"
              required
              aria-invalid={errores.dni !== undefined}
              aria-describedby={errores.dni ? 'error-dni' : undefined}
              title="Ingresá solamente los dígitos del DNI, sin puntos ni espacios."
            />
            {mostrarErrorDeCampo('dni')}
          </div>

          <div className={`campo ${errores.fechaNacimiento ? 'con-error' : ''}`}>
            <label htmlFor="fechaNacimiento">Fecha de nacimiento</label>
            <input
              id="fechaNacimiento"
              name="fechaNacimiento"
              type="date"
              autoComplete="bday"
              value={datos.fechaNacimiento}
              onChange={(e) => actualizar('fechaNacimiento', e.target.value)}
              required
              aria-invalid={errores.fechaNacimiento !== undefined}
              aria-describedby={errores.fechaNacimiento ? 'error-fechaNacimiento' : undefined}
            />
            {mostrarErrorDeCampo('fechaNacimiento')}
          </div>

          <div className={`campo ${errores.telefono ? 'con-error' : ''}`}>
            <label htmlFor="telefono">Teléfono</label>
            <input
              id="telefono"
              name="telefono"
              type="tel"
              inputMode="tel"
              autoComplete="tel"
              value={datos.telefono}
              onChange={(e) => actualizar('telefono', e.target.value)}
              maxLength={30}
              pattern="(?=.*[0-9])[0-9+(). -]{6,30}"
              title="Ingresá un teléfono con números y sin letras."
              required
              aria-invalid={errores.telefono !== undefined}
              aria-describedby={errores.telefono ? 'error-telefono' : undefined}
            />
            {mostrarErrorDeCampo('telefono')}
          </div>

          <div className={`campo ${errores.email ? 'con-error' : ''}`}>
            <label htmlFor="email">Email</label>
            <input
              id="email"
              name="email"
              type="email"
              autoComplete="email"
              value={datos.email}
              onChange={(e) => actualizar('email', e.target.value)}
              maxLength={254}
              required
              aria-invalid={errores.email !== undefined}
              aria-describedby={errores.email ? 'error-email' : undefined}
            />
            {mostrarErrorDeCampo('email')}
          </div>
        </div>

        <p className={clasesDeAvisoDePantalla.nota} role="status">
          El tipo de persona se define como Empleado en el servidor. No se puede convertir un chofer desde este formulario.
        </p>

        <BarraDeAcciones anclaje="viewport" leyenda={LEYENDA_DE_OBLIGATORIOS}>
          <Boton variante="secundario" onClick={() => navegar('/empleados')}>
            Cancelar
          </Boton>
          <Boton type="submit" variante="primario" disabled={guardando} icono={<IconoEnRegla className="size-3" />}>
            {guardando ? 'Guardando…' : esEdicion ? 'Guardar cambios' : 'Guardar empleado'}
          </Boton>
        </BarraDeAcciones>
      </form>
    </section>
  )
}
