import type { ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { Menu } from './Menu'
import { Boton } from './ui/Boton'
import { IconoCerrarSesion, IconoContrasena } from './ui/iconos'
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
 * El botón de cerrar sesión vive acá, y no en cada pantalla, porque FR-013 del Módulo 1 exige que
 * esté disponible desde cualquier lugar del sistema.
 *
 * El enlace *Cambiar contraseña* también va acá, y **no** en el menú: ese menú lo calcula el
 * servidor a partir de los permisos, y cambiar la contraseña propia no exige ninguno (FR-029). Todo
 * usuario autenticado tiene que poder llegar, tenga el rol que tenga.
 *
 * **La jerarquía es lo que cambió** (FR-016 del Módulo 7): antes la marca, el usuario, *Cambiar
 * contraseña* y *Cerrar sesión* iban en una sola línea con el mismo peso, y cerrar la sesión pesaba
 * lo mismo que la acción principal de la pantalla. Ahora las acciones de cuenta son secundarias y
 * discretas, y quien manda visualmente es el encabezado de la pantalla que está debajo.
 */
export function Layout({ username, opcionesMenu, onCerrarSesion, children }: Props) {
  return (
    <div className="flex min-h-screen flex-col bg-pagina">
      <header className="flex flex-wrap items-center justify-between gap-4 border-b border-borde bg-superficie px-6 py-3">
        <Link
          to="/"
          className="text-base font-semibold text-texto no-underline hover:text-acento"
        >
          Sistema Integral de Gestión
        </Link>

        <div className="flex items-center gap-3">
          <span className="text-sm text-texto-suave">{username}</span>

          <Link
            to="/mi-cuenta/contrasena"
            className="inline-flex items-center gap-1.5 text-sm text-acento underline underline-offset-2 hover:text-acento-oscuro"
          >
            <IconoContrasena aria-hidden="true" className="size-4" />
            Cambiar contraseña
          </Link>

          <Boton variante="secundario" tamanio="chico" onClick={onCerrarSesion}>
            <IconoCerrarSesion aria-hidden="true" className="size-4" />
            Cerrar sesión
          </Boton>
        </div>
      </header>

      <div className="flex flex-1 flex-col md:flex-row">
        <Menu opciones={opcionesMenu} />

        <main className="min-w-0 flex-1 px-6 py-6">
          <div className="mx-auto max-w-lectura">{children}</div>
        </main>
      </div>
    </div>
  )
}
