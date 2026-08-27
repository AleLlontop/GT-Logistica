import type { ReactNode } from 'react'
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
 * **La barra superior se retira** (FR-010). Era la última superficie del sistema que no era ni la
 * navegación ni el contenido: una franja blanca con la marca a la izquierda y las acciones de cuenta
 * a la derecha, que le comía 56 px de alto a todas las pantallas para repetir lo que la isla de
 * navegación ya puede decir. La marca sube al encabezado de la isla y el bloque de usuario —con
 * *Cambiar contraseña* y *Cerrar sesión*— baja a su pie.
 *
 * El botón de cerrar sesión sigue estando disponible desde cualquier lugar del sistema (FR-013 del
 * Módulo 1) y *Cambiar contraseña* sigue **fuera** del menú calculado por el servidor: ese menú sale
 * de los permisos, y cambiar la contraseña propia no exige ninguno (FR-029 del Módulo 2). Lo que
 * cambia es dónde viven, no que existan.
 *
 * **La columna principal queda transparente** (FR-011): el lienzo se ve a través. Eso convierte al
 * lienzo en un fondo de texto real, y por eso acá sólo puede ir texto en `ink` o `ink-soft`
 * (FR-007b). Los dos únicos textos que quedan directamente sobre él son el título de página y su
 * bajada, que `EncabezadoDePantalla` dibuja en esos dos tonos.
 */
export function Layout({ username, opcionesMenu, onCerrarSesion, children }: Props) {
  return (
    <div className="flex min-h-screen gap-[18px] p-[18px]">
      <Menu opciones={opcionesMenu} username={username} onCerrarSesion={onCerrarSesion} />

      <main className="min-w-0 flex-1 px-10 py-6">
        <div className="mx-auto max-w-lectura">{children}</div>
      </main>
    </div>
  )
}
