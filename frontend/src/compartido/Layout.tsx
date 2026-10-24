import { useState, type ReactNode } from 'react'
import { Link, useLocation } from 'react-router-dom'
import * as Dialog from '@radix-ui/react-dialog'
import { ChevronRight, LogOut, Menu as IconoMenu, X } from 'lucide-react'
import { Menu } from './Menu'
import { SelectorTema } from './SelectorTema'
import { agruparEnSecciones } from './seccionesDeMenu'
import type { OpcionMenu } from './tipos'

interface Props {
  username: string
  opcionesMenu: OpcionMenu[]
  onCerrarSesion: () => void
  children: ReactNode
  adaptable?: boolean
}

/** Shell común: navegación persistente en escritorio y drawer accesible en móvil.
 * El contexto se deriva del menú existente; no agrega permisos ni solicitudes. */
export function Layout({ username, opcionesMenu, onCerrarSesion, children }: Props) {
  const [menuAbierto, setMenuAbierto] = useState(false)
  const { pathname } = useLocation()
  const opcion = [...opcionesMenu].sort((a, b) => b.ruta.length - a.ruta.length)
    .find(o => pathname === o.ruta || pathname.startsWith(o.ruta + '/'))
  const seccion = agruparEnSecciones(opcionesMenu).find(s => s.opciones.some(o => o.codigo === opcion?.codigo))
  const contexto = pathname === '/' ? 'Inicio' : pathname === '/mi-cuenta/contrasena' ? 'Cambiar contraseña' : opcion?.etiqueta ?? 'Gestión'

  return <div className="flex min-h-dvh">
    <a href="#contenido-principal" className="gyt-skip">Ir al contenido</a>
    <aside className="sticky top-0 hidden h-dvh w-[256px] shrink-0 lg:block"><Menu opciones={opcionesMenu} /></aside>
    <div className="min-w-0 flex-1">
      <header className="sticky top-0 z-30 flex h-[72px] items-center justify-between gap-3 gyt-topbar border-b px-4 sm:px-7 lg:px-8">
        <div className="flex min-w-0 items-center gap-3">
          <Dialog.Root open={menuAbierto} onOpenChange={setMenuAbierto}>
            <Dialog.Trigger asChild><button type="button" className="gyt-icon-button lg:hidden" aria-label="Abrir menú"><IconoMenu aria-hidden="true" className="size-[18px]" /></button></Dialog.Trigger>
            <Dialog.Portal>
              <Dialog.Overlay className="fixed inset-0 z-40 bg-overlay backdrop-blur-sm lg:hidden" />
              <Dialog.Content className="gyt-sidebar fixed inset-y-0 left-0 z-50 w-[min(300px,calc(100vw-2rem))] shadow-island lg:hidden" aria-describedby={undefined}>
                <Dialog.Title className="sr-only">Navegación</Dialog.Title>
                <Dialog.Close asChild><button type="button" aria-label="Cerrar menú" className="absolute top-2 right-2 z-10 rounded-field p-1 text-sidebar-text transition-colors duration-150 hover:bg-sidebar-hover active:bg-sidebar-active"><X aria-hidden="true" className="size-4" /></button></Dialog.Close>
                <Menu opciones={opcionesMenu} onNavegar={() => setMenuAbierto(false)} />
              </Dialog.Content>
            </Dialog.Portal>
          </Dialog.Root>
          <nav aria-label="Ubicación actual" className="flex min-w-0 items-center gap-2 text-xs">
            {contexto !== 'Inicio' && <Link to="/" className="hidden rounded-field gyt-topbar-link no-underline transition-colors duration-150 sm:inline">Inicio</Link>}
            {contexto !== 'Inicio' && <ChevronRight aria-hidden="true" className="hidden size-3.5 shrink-0 text-topbar-muted sm:block" />}
            {seccion && seccion.nombre !== contexto && <span className="hidden items-center gap-2 text-topbar-muted xl:flex">{seccion.nombre}<ChevronRight aria-hidden="true" className="size-3.5" /></span>}
            <span className="truncate font-semibold text-topbar-text">{contexto}</span>
          </nav>
        </div>
        <div className="flex shrink-0 items-center gap-2 sm:gap-3">
          <SelectorTema />
          <div className="mx-1 h-6 border-l border-topbar-border" aria-hidden="true" />
          <span className="flex size-8 items-center justify-center rounded-chip bg-topbar-hover font-mono text-[11px] font-semibold text-topbar-text" aria-hidden="true">{username.slice(0, 2).toUpperCase()}</span>
          <span className="hidden max-w-36 truncate text-xs font-semibold text-topbar-text sm:block">{username}</span>
          <button type="button" onClick={onCerrarSesion} aria-label="Cerrar sesión" title="Cerrar sesión" className="gyt-icon-button ml-1 px-2.5 text-xs font-medium">
            <LogOut aria-hidden="true" className="size-4" /><span className="hidden xl:inline">Cerrar sesión</span>
          </button>
        </div>
      </header>
      <main id="contenido-principal" tabIndex={-1} className="min-w-0 px-4 py-7 outline-none sm:px-7 lg:px-8 lg:py-8">
        <div className="mx-auto max-w-lectura">{children}</div>
      </main>
    </div>
  </div>
}
