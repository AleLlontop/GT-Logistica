import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Menu } from './Menu'
import type { OpcionMenu } from './tipos'

interface Props {
  username: string
  opcionesMenu: OpcionMenu[]
  onCerrarSesion: () => void
  children: ReactNode
}

/**
 * Estructura común a todas las pantallas con sesión abierta.
 *
 * El botón de cerrar sesión vive acá, y no en cada pantalla, porque FR-013 exige que esté
 * disponible desde cualquier lugar del sistema.
 *
 * El enlace *Cambiar contraseña* también va acá, y **no** en el menú: ese menú lo calcula el
 * servidor a partir de los permisos, y cambiar la contraseña propia no exige ninguno (FR-029). Todo
 * usuario autenticado tiene que poder llegar, tenga el rol que tenga.
 */
export function Layout({ username, opcionesMenu, onCerrarSesion, children }: Props) {
  return (
    <div className="layout">
      <header className="layout__encabezado">
        <span className="layout__marca">Sistema Integral de Gestión</span>

        <div className="layout__sesion">
          <span>{username}</span>
          <Link to="/mi-cuenta/contrasena">Cambiar contraseña</Link>
          <button type="button" onClick={onCerrarSesion}>
            Cerrar sesión
          </button>
        </div>
      </header>

      <Menu opciones={opcionesMenu} />

      <main>{children}</main>
    </div>
  )
}
