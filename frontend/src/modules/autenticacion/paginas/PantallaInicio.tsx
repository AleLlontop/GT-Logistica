import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { Link } from 'react-router-dom'
import { ArrowUpRight, Compass } from 'lucide-react'
import { iconoDeRuta } from '../../../compartido/iconosDeNavegacion'
import { accesosPrioritarios } from '../../../compartido/seccionesDeMenu'
import type { Sesion } from '../servicios/sesion'

interface Props { sesion: Sesion }

/** Portada editorial, sin métricas inventadas ni consultas de negocio adicionales. */
export function PantallaInicio({ sesion }: Props) {
  const accesos = accesosPrioritarios(sesion.opcionesMenu)
  return <section>
    <div className="relative mb-8 overflow-hidden rounded-card border border-line bg-surface p-6 sm:p-8">
      <span aria-hidden="true" className="absolute inset-y-0 left-0 w-1 bg-brand-fill" />
      <div className="relative max-w-2xl">
        <p className="m-0 mb-4 font-mono text-[10px] font-semibold tracking-[0.16em] text-brand uppercase">G&amp;T Logística · Centro de operaciones</p>
        <EncabezadoDePantalla className="mb-0" titulo={`Hola, ${sesion.username}`} resumen={<>{sesion.roles.length === 1 ? 'Tu rol: ' : 'Tus roles: '}{sesion.roles.map(r => r.nombre).join(', ')}</>} />
        <p className="mt-5 mb-0 max-w-lg text-sm leading-6 text-ink-soft">Tu operación, en un solo lugar. Accedé a las tareas principales o explorá los módulos desde el menú.</p>
      </div>
      <Compass aria-hidden="true" className="absolute top-8 right-8 hidden size-28 text-brand/10 xl:block" strokeWidth={1} />
    </div>
    {accesos.length === 0 ? <p className="max-w-prose text-sm leading-6 text-ink-soft">Por ahora no tenés funcionalidades disponibles. Se irán agregando a medida que se implementen.</p> : <>
      <div className="mb-4 flex flex-wrap items-end justify-between gap-2">
        <div><h2 className="m-0 text-lg font-bold tracking-tight text-ink">Accesos rápidos</h2><p className="mt-1 mb-0 text-xs text-ink-soft">Las herramientas principales para tu jornada.</p></div>
        <span className="text-xs text-faint">Todos los módulos, en el menú lateral</span>
      </div>
      <ul className="m-0 grid list-none grid-cols-1 gap-4 p-0 sm:grid-cols-2 xl:grid-cols-4">
        {accesos.map((opcion, indice) => {
          const Icono = iconoDeRuta(opcion.ruta)
          return <li key={opcion.codigo}><Link to={opcion.ruta} className="gyt-quick-card group" data-featured={indice === 0 || undefined}>
            <span className="flex items-center justify-between gap-3"><span className="flex size-10 items-center justify-center rounded-chip bg-brand-soft text-brand"><Icono aria-hidden="true" className="size-5" strokeWidth={1.7} /></span><ArrowUpRight aria-hidden="true" className="size-4 text-faint transition-transform duration-150 group-hover:translate-x-0.5 group-hover:-translate-y-0.5" /></span>
            <span className="mt-5 block text-sm font-semibold">{opcion.etiqueta}</span>
            <span className="mt-1.5 block text-xs text-ink-soft">Abrir módulo</span>
          </Link></li>
        })}
      </ul>
    </>}
  </section>
}
