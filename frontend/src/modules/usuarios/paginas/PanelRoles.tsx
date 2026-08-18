import { clasesDeFormulario } from '../../../compartido/ui/clases'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { useEffect, useState, type FormEvent } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import type { CodigoRol, RolConPermisos } from '../../../compartido/tipos'
import { PermisosDelRol } from '../componentes/PermisosDelRol'
import { asignarRoles, listarRoles, obtenerUsuario } from '../servicios/usuarios'

const MENSAJE_SIN_ROLES = 'Todo usuario tiene que tener al menos un rol asignado.'

/**
 * Panel de roles de un usuario (User Story 4).
 *
 * Guardar **reemplaza** la selección: los roles quedan exactamente como se dejaron marcados, ni más
 * ni menos (FR-018). Desmarcar todos se rechaza (FR-001), y quitarle el rol de administrador al
 * único que queda activo también (FR-019).
 */
export function PanelRoles() {
  const navegar = useNavigate()
  const { id } = useParams<{ id: string }>()
  const idUsuario = Number(id)

  const [username, setUsername] = useState('')
  const [roles, setRoles] = useState<RolConPermisos[]>([])
  const [marcados, setMarcados] = useState<CodigoRol[]>([])
  const [verPermisosDe, setVerPermisosDe] = useState<RolConPermisos | null>(null)

  const [cargando, setCargando] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [enviando, setEnviando] = useState(false)

  useEffect(() => {
    let vigente = true

    Promise.all([obtenerUsuario(idUsuario), listarRoles()])
      .then(([usuario, catalogo]) => {
        if (vigente) {
          setUsername(usuario.username)
          setMarcados(usuario.roles.map((rol) => rol.codigo))
          setRoles(catalogo)
        }
      })
      .catch((fallo) => {
        if (vigente) {
          setError(
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

  function alternar(codigo: CodigoRol) {
    setMarcados((actuales) =>
      actuales.includes(codigo)
        ? actuales.filter((rol) => rol !== codigo)
        : [...actuales, codigo],
    )
  }

  async function alGuardar(evento: FormEvent) {
    evento.preventDefault()
    setError(null)

    // FR-001: se resuelve en pantalla, sin molestar al servidor.
    if (marcados.length === 0) {
      setError(MENSAJE_SIN_ROLES)
      return
    }

    setEnviando(true)

    try {
      await asignarRoles(idUsuario, marcados)
      navegar(`/usuarios/${idUsuario}`, { replace: true })
    } catch (fallo) {
      // Acá cae el rechazo de FR-019: quitarle el rol al último administrador activo.
      setError(
        fallo instanceof ErrorHttp
          ? fallo.detalle.mensaje
          : 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.',
      )
    } finally {
      setEnviando(false)
    }
  }

  if (cargando) {
    return (
      <section>
        <p role="status">Cargando roles…</p>
      </section>
    )
  }

  return (
    <section>
      <EncabezadoDePantalla titulo={`Roles de ${username}`} />

      {error !== null && (
        <p className="formulario__error" role="alert">
          {error}
        </p>
      )}

      <form onSubmit={alGuardar} noValidate className={clasesDeFormulario}>
        <fieldset>
          <legend>Roles del sistema</legend>

          {roles.map((rol) => (
            <div key={rol.codigo}>
              <input
                id={`rol-${rol.codigo}`}
                type="checkbox"
                checked={marcados.includes(rol.codigo)}
                onChange={() => alternar(rol.codigo)}
              />
              <label htmlFor={`rol-${rol.codigo}`}>{rol.nombre}</label>

              <button type="button" onClick={() => setVerPermisosDe(rol)}>
                Ver permisos
              </button>
            </div>
          ))}
        </fieldset>

        <button type="submit" disabled={enviando}>
          {enviando ? 'Guardando…' : 'Guardar'}
        </button>

        <Link to={`/usuarios/${idUsuario}`}>Volver al detalle</Link>
      </form>

      {verPermisosDe !== null && (
        <PermisosDelRol rol={verPermisosDe} onCerrar={() => setVerPermisosDe(null)} />
      )}
    </section>
  )
}
