import { useState, type ReactNode } from 'react'
import { Menu } from './Menu'
import type { OpcionMenu } from './tipos'
import { IconoCerrar, IconoMenu } from './ui/iconos'
import { InterruptorModoOscuro } from './ui/InterruptorModoOscuro'

interface Props {
  username: string
  opcionesMenu: OpcionMenu[]
  onCerrarSesion: () => void
  children: ReactNode
}

/**
 * Estructura común a todas las pantallas con sesión abierta.
 *
 * Adaptable a dispositivos móviles y escritorio (responsive).
 */
export function Layout({ username, opcionesMenu, onCerrarSesion, children }: Props) {
  const [menuMovilAbierto, setMenuMovilAbierto] = useState(false)

  return (
    <div className="min-h-screen text-ink">
      {/* Barra superior en pantallas chicas (< lg) */}
      <header className="sticky top-0 z-30 flex items-center justify-between border-b border-line bg-surface px-4 py-2.5 shadow-sm lg:hidden">
        <div className="flex items-center gap-3">
          <button
            type="button"
            aria-label={menuMovilAbierto ? 'Cerrar menú' : 'Abrir menú'}
            onClick={() => setMenuMovilAbierto((prev) => !prev)}
            className="flex size-8 items-center justify-center rounded-pastilla border border-line bg-surface-mute text-ink hover:bg-surface-soft transition-colors cursor-pointer"
          >
            {menuMovilAbierto ? (
              <IconoCerrar className="size-4" aria-hidden="true" />
            ) : (
              <IconoMenu className="size-4" aria-hidden="true" />
            )}
          </button>
          <span className="text-[13.5px] font-bold tracking-tight text-ink">
            Sistema Integral de Gestión
          </span>
        </div>

        <InterruptorModoOscuro />
      </header>

      <div className="flex min-h-screen gap-[18px] p-3 md:p-[18px]">
        {/* Menú en escritorio (isla flotante fija) */}
        <aside className="hidden lg:block shrink-0">
          <Menu opciones={opcionesMenu} username={username} onCerrarSesion={onCerrarSesion} />
        </aside>

        {/* Menú en móvil / tablet (drawer flotante con backdrop) */}
        {menuMovilAbierto && (
          <div
            className="fixed inset-0 z-50 flex lg:hidden bg-black/60 backdrop-blur-xs p-3"
            onClick={() => setMenuMovilAbierto(false)}
          >
            <div
              className="relative max-h-full overflow-y-auto"
              onClick={(e) => e.stopPropagation()}
            >
              <Menu
                opciones={opcionesMenu}
                username={username}
                onCerrarSesion={() => {
                  setMenuMovilAbierto(false)
                  onCerrarSesion()
                }}
                onNavegar={() => setMenuMovilAbierto(false)}
                onCerrar={() => setMenuMovilAbierto(false)}
              />
            </div>
          </div>
        )}

        <main className="min-w-0 flex-1 px-1 py-2 sm:px-4 sm:py-3 md:px-6 md:py-6 lg:px-10">
          <div className="mx-auto max-w-lectura">{children}</div>
        </main>
      </div>
    </div>
  )
}
