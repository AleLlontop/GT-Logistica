import { useEffect, useId, useState } from 'react'
import { Link, NavLink, useLocation } from 'react-router-dom'
import { ArrowUpRight, ChevronDown, House, KeyRound } from 'lucide-react'
import { agruparEnSecciones } from './seccionesDeMenu'
import { iconoDeRuta } from './iconosDeNavegacion'
import type { OpcionMenu } from './tipos'

interface Props { opciones: OpcionMenu[]; onNavegar?: () => void }

/** Los módulos pliegan presentación, nunca autorizaciones. La ruta abre su grupo al navegar. */
export function Menu({ opciones, onNavegar }: Props) {
  const secciones = agruparEnSecciones(opciones)
  const { pathname } = useLocation()
  const id = useId()
  const actual = [...opciones].sort((a, b) => b.ruta.length - a.ruta.length)
    .find(o => pathname === o.ruta || pathname.startsWith(o.ruta + '/'))
  const grupoActual = secciones.find(s => s.opciones.some(o => o.codigo === actual?.codigo))?.nombre
  const [grupoAbierto, setGrupoAbierto] = useState<string | null>(grupoActual ?? null)
  useEffect(() => {
    setGrupoAbierto(grupoActual ?? null)
  }, [pathname, grupoActual])

  return <div className="gyt-sidebar flex h-full min-h-0 flex-col">
    <div className="flex h-[100px] shrink-0 items-center border-b border-sidebar-border px-6">
      <Link to="/" onClick={onNavegar} aria-label="G&T Logística · Sistema Integral de Gestión" className="gyt-brand-lockup flex flex-col gap-2 rounded-field no-underline">
        <span aria-hidden="true" className="flex items-baseline gap-2"><span className="text-[30px] leading-none font-extrabold tracking-[-0.06em] text-sidebar-ink">G&amp;T<span className="text-brand-fill">.</span></span><span className="text-[11px] font-semibold tracking-[0.12em] text-sidebar-text uppercase">Logística</span></span>
        <span className="flex items-center gap-2 text-[9px] tracking-wide text-sidebar-muted"><span aria-hidden="true" className="gyt-brand-rule" />Sistema Integral de Gestión</span>
      </Link>
    </div>
    <nav aria-label="Menú principal" className="min-h-0 flex-1 overflow-y-auto px-3 py-4">
      <NavLink to="/" end onClick={onNavegar} className="gyt-navlink mb-4"><House aria-hidden="true" className="size-4" /><span>Inicio</span></NavLink>
      {secciones.length > 0 && <>
        <p className="mb-2 px-3 text-[9px] font-bold tracking-[0.17em] text-sidebar-muted uppercase">Módulos de gestión</p>
        <ul className="m-0 flex list-none flex-col gap-1 p-0">{secciones.map((seccion, indice) => {
          const Icono = iconoDeRuta(seccion.opciones[0].ruta)
          const abierto = grupoAbierto === seccion.nombre
          const contieneActual = grupoActual === seccion.nombre
          const panelId = `${id}-modulo-${indice}`
          return <li key={seccion.nombre}>
            <button type="button" className="gyt-module-trigger" data-current={contieneActual || undefined} aria-expanded={abierto} aria-controls={panelId} onClick={() => setGrupoAbierto(abierto ? null : seccion.nombre)}>
              <Icono aria-hidden="true" className="size-4 shrink-0" strokeWidth={1.7} /><span className="flex-1 text-left">{seccion.nombre}</span><ChevronDown aria-hidden="true" className={`size-3.5 shrink-0 transition-transform duration-150 ${abierto ? 'rotate-180' : ''}`} />
            </button>
            <div id={panelId} hidden={!abierto} className="ml-5 mt-1 mb-2 border-l border-sidebar-border pl-2">
              <ul className="m-0 flex list-none flex-col gap-0.5 p-0">{seccion.opciones.map(opcion => <li key={opcion.codigo}>
                <NavLink to={opcion.ruta} end={actual?.ruta !== opcion.ruta} onClick={onNavegar} className="gyt-navlink"><span>{opcion.etiqueta}</span></NavLink>
              </li>)}</ul>
            </div>
          </li>
        })}</ul>
      </>}
    </nav>
    <div className="mt-auto shrink-0 border-t border-sidebar-border p-3">
      <Link to="/mi-cuenta/contrasena" onClick={onNavegar} className="gyt-navlink"><KeyRound aria-hidden="true" className="size-4 shrink-0" /><span>Cambiar contraseña</span><ArrowUpRight aria-hidden="true" className="ml-auto size-3.5" /></Link>
    </div>
  </div>
}
