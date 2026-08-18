import { clasesDeFormulario } from '../../../compartido/ui/clases'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { useEffect, useState, type FormEvent } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import type { CodigoRol, EstadoUsuario } from '../../../compartido/tipos'
import { SelectorPersona } from '../componentes/SelectorPersona'
import {
  crearUsuario,
  ESTADOS_DE_USUARIO,
  modificarUsuario,
  obtenerUsuario,
  ROLES_DEL_SISTEMA,
} from '../servicios/usuarios'

const LARGO_MINIMO_PASSWORD = 8

const MENSAJE_EMAIL_INVALIDO = 'Escribí un email válido, con formato nombre@dominio.'
const MENSAJE_PASSWORD_CORTA = `La contraseña tiene que tener al menos ${LARGO_MINIMO_PASSWORD} caracteres.`
const MENSAJE_USERNAME_VACIO = 'Escribí un nombre de usuario.'
const MENSAJE_SIN_ROLES = 'Todo usuario tiene que tener al menos un rol asignado.'

/** Errores de formato por campo, para marcarlos en rojo en el lugar correcto. */
type ErroresDeCampo = Partial<Record<'username' | 'email' | 'password' | 'roles', string>>

/** Campos que esta pantalla puede marcar. El backend puede señalar otros, como `personaId`. */
const CAMPOS_DEL_FORMULARIO: readonly string[] = ['username', 'email', 'password', 'roles']

/**
 * Alta y edición de un usuario (User Story 1 y User Story 3).
 *
 * En edición **no hay ningún campo de contraseña** (FR-014): para cambiarla está el restablecimiento,
 * que vive en la pantalla de detalle. Los roles tampoco se editan acá, sino en el panel de roles.
 *
 * La validación de formato se resuelve en pantalla y no llega al servidor; las reglas de negocio
 * —duplicados, persona ya vinculada, último administrador— las decide el backend y se muestran con su
 * texto tal cual, sobre el campo que indique la respuesta.
 */
export function FormularioUsuario() {
  const navegar = useNavigate()
  const { id } = useParams<{ id: string }>()

  const esEdicion = id !== undefined
  const idUsuario = esEdicion ? Number(id) : null

  const [username, setUsername] = useState('')
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  // FR-005: en el alta, el estado viene precargado en `activo`.
  const [estado, setEstado] = useState<EstadoUsuario>('activo')
  const [roles, setRoles] = useState<CodigoRol[]>([])
  const [personaId, setPersonaId] = useState<number | null>(null)

  const [cargando, setCargando] = useState(esEdicion)
  const [errores, setErrores] = useState<ErroresDeCampo>({})
  const [errorGeneral, setErrorGeneral] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  // FR-014: el formulario de edición abre con los datos actuales cargados.
  useEffect(() => {
    if (idUsuario === null) {
      return
    }

    let vigente = true

    obtenerUsuario(idUsuario)
      .then((usuario) => {
        if (!vigente) {
          return
        }

        setUsername(usuario.username)
        setEmail(usuario.email)
        setEstado(usuario.estado)
        setRoles(usuario.roles.map((rol) => rol.codigo))
        setPersonaId(usuario.persona?.id ?? null)
      })
      .catch((fallo) => {
        if (vigente) {
          setErrorGeneral(
            fallo instanceof ErrorHttp
              ? fallo.detalle.mensaje
              : 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.',
          )
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
  }, [idUsuario])

  function alternarRol(codigo: CodigoRol) {
    setRoles((actuales) =>
      actuales.includes(codigo)
        ? actuales.filter((rol) => rol !== codigo)
        : [...actuales, codigo],
    )
  }

  /** Devuelve los errores de formato; vacío significa que se puede llamar al servidor. */
  function validarEnPantalla(): ErroresDeCampo {
    const encontrados: ErroresDeCampo = {}

    if (username.trim() === '') {
      encontrados.username = MENSAJE_USERNAME_VACIO
    }

    // Misma validación laxa que el servidor: algo, una arroba, algo con un punto. No se intenta
    // implementar el RFC.
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email.trim())) {
      encontrados.email = MENSAJE_EMAIL_INVALIDO
    }

    if (!esEdicion && password.length < LARGO_MINIMO_PASSWORD) {
      encontrados.password = MENSAJE_PASSWORD_CORTA
    }

    // En edición los roles se cambian desde el panel de roles, no acá.
    if (!esEdicion && roles.length === 0) {
      encontrados.roles = MENSAJE_SIN_ROLES
    }

    return encontrados
  }

  async function alEnviar(evento: FormEvent) {
    evento.preventDefault()

    const deFormato = validarEnPantalla()
    setErrores(deFormato)
    setErrorGeneral(null)

    // No se llama al servidor hasta que el formulario es válido.
    if (Object.keys(deFormato).length > 0) {
      return
    }

    setEnviando(true)

    try {
      if (idUsuario === null) {
        await crearUsuario({
          username: username.trim(),
          email: email.trim(),
          password,
          estado,
          roles,
          personaId,
        })
      } else {
        await modificarUsuario(idUsuario, {
          username: username.trim(),
          email: email.trim(),
          estado,
          personaId,
        })
      }

      navegar(idUsuario === null ? '/usuarios' : `/usuarios/${idUsuario}`, { replace: true })
    } catch (fallo) {
      if (fallo instanceof ErrorHttp) {
        // Si la respuesta identifica un campo del formulario, el mensaje va sobre ese campo; si no
        // —o si señala uno que acá no existe, como `personaId`—, va arriba del formulario.
        const campo = fallo.detalle.campo

        if (campo !== undefined && CAMPOS_DEL_FORMULARIO.includes(campo)) {
          setErrores({ [campo as keyof ErroresDeCampo]: fallo.detalle.mensaje })
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
      <section>
        <p role="status">Cargando usuario…</p>
      </section>
    )
  }

  return (
    <section>
      <EncabezadoDePantalla titulo={esEdicion ? 'Editar usuario' : 'Nuevo usuario'} />

      <form onSubmit={alEnviar} noValidate className={clasesDeFormulario}>
        {errorGeneral !== null && (
          <p className="formulario__error" role="alert">
            {errorGeneral}
          </p>
        )}

        <div className="campo">
          <label htmlFor="username">Nombre de usuario</label>
          <input
            id="username"
            name="username"
            type="text"
            value={username}
            onChange={(evento) => setUsername(evento.target.value)}
            autoComplete="off"
            required
            aria-required="true"
            aria-invalid={errores.username !== undefined}
            aria-describedby={errores.username !== undefined ? 'error-username' : undefined}
          />
          {errores.username !== undefined && (
            <p className="campo__error" id="error-username" role="alert">
              {errores.username}
            </p>
          )}
        </div>

        <div className="campo">
          <label htmlFor="email">Email</label>
          <input
            id="email"
            name="email"
            type="email"
            value={email}
            onChange={(evento) => setEmail(evento.target.value)}
            autoComplete="off"
            required
            aria-required="true"
            aria-invalid={errores.email !== undefined}
            aria-describedby={errores.email !== undefined ? 'error-email' : undefined}
          />
          {errores.email !== undefined && (
            <p className="campo__error" id="error-email" role="alert">
              {errores.email}
            </p>
          )}
        </div>

        {/* FR-014: en edición no aparece ningún campo de contraseña. */}
        {!esEdicion && (
          <div className="campo">
            <label htmlFor="password">Contraseña inicial</label>
            {/* Siempre enmascarada, sin botón de "ver" (FR-004). */}
            <input
              id="password"
              name="password"
              type="password"
              value={password}
              onChange={(evento) => setPassword(evento.target.value)}
              autoComplete="new-password"
              required
              aria-required="true"
              aria-invalid={errores.password !== undefined}
              aria-describedby={errores.password !== undefined ? 'error-password' : undefined}
            />
            {errores.password !== undefined && (
              <p className="campo__error" id="error-password" role="alert">
                {errores.password}
              </p>
            )}
          </div>
        )}

        <div className="campo">
          <label htmlFor="estado">Estado</label>
          <select
            id="estado"
            name="estado"
            value={estado}
            onChange={(evento) => setEstado(evento.target.value as EstadoUsuario)}
          >
            {ESTADOS_DE_USUARIO.map((opcion) => (
              <option key={opcion.codigo} value={opcion.codigo}>
                {opcion.nombre}
              </option>
            ))}
          </select>
        </div>

        <SelectorPersona valor={personaId} onCambio={setPersonaId} deshabilitado={enviando} />

        {!esEdicion && (
          <fieldset
            className="campo"
            aria-invalid={errores.roles !== undefined}
            aria-describedby={errores.roles !== undefined ? 'error-roles' : undefined}
          >
            <legend>Roles</legend>

            {ROLES_DEL_SISTEMA.map((rol) => (
              <div key={rol.codigo}>
                <input
                  id={`rol-${rol.codigo}`}
                  name="roles"
                  type="checkbox"
                  value={rol.codigo}
                  checked={roles.includes(rol.codigo)}
                  onChange={() => alternarRol(rol.codigo)}
                />
                <label htmlFor={`rol-${rol.codigo}`}>{rol.nombre}</label>
              </div>
            ))}

            {errores.roles !== undefined && (
              <p className="campo__error" id="error-roles" role="alert">
                {errores.roles}
              </p>
            )}
          </fieldset>
        )}

        <button type="submit" disabled={enviando}>
          {enviando ? 'Guardando…' : 'Guardar'}
        </button>
      </form>
    </section>
  )
}
