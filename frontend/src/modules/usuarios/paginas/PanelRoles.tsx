import { BarraDeAcciones } from '../../../compartido/ui/BarraDeAcciones'
import { Boton } from '../../../compartido/ui/Boton'
import { clasesDeBoton } from '../../../compartido/ui/clases'
import { IconoEnRegla } from '../../../compartido/ui/iconos'
import { clasesDeFormularioSimple } from '../../../compartido/ui/clases'
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
        <p role="status" className="m-0 text-[13px] text-ink-soft">Cargando roles…</p>
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

      <form onSubmit={alGuardar} noValidate className={clasesDeFormularioSimple}>
        <fieldset className="flex flex-col gap-3">
          <legend className="mb-4 font-semibold text-[13.5px] text-ink-soft">Roles del sistema</legend>

          {roles.map((rol) => (
            <div key={rol.codigo} className="flex items-center gap-2.5">
              <input
                id={`rol-${rol.codigo}`}
                type="checkbox"
                checked={marcados.includes(rol.codigo)}
                onChange={() => alternar(rol.codigo)}
              />
              <label htmlFor={`rol-${rol.codigo}`} className="text-[13.5px] font-medium text-ink cursor-pointer">
                {rol.nombre}
              </label>

              <button 
                type="button" 
                className={clasesDeBoton('texto', 'chico')} 
                onClick={() => setVerPermisosDe(rol)}
              >
                Ver permisos
              </button>
            </div>
          ))}
        </fieldset>

        <BarraDeAcciones anclaje="viewport">
          {/* Navega, así que sigue siendo un `<a>` aunque se vea como un botón (FR-023). */}
          <Link to={`/usuarios/${idUsuario}`} className={clasesDeBoton('secundario')}>
            Volver al detalle
          </Link>
          <Boton
            type="submit"
            variante="primario"
            disabled={enviando}
            icono={<IconoEnRegla className="size-3" />}
          >
            {enviando ? 'Guardando…' : 'Guardar'}
          </Boton>
        </BarraDeAcciones>
      </form>

      {verPermisosDe !== null && (
        <PermisosDelRol rol={verPermisosDe} onCerrar={() => setVerPermisosDe(null)} />
      )}
    </section>
  )
}
