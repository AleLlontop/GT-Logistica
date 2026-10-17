import type { ReactNode } from 'react'
import { IconoBuscar, IconoDocumento, IconoPendiente } from '../../../compartido/ui/iconos'

// Conserva el tratamiento de GT UI sin mover la región viva al contenedor decorativo.
export function MensajeListado({ caso, children }: { caso: 'cargando' | 'vacio' | 'sinCoincidencias'; children: ReactNode }) {
  const Icono = caso === 'cargando' ? IconoPendiente : caso === 'vacio' ? IconoDocumento : IconoBuscar
  return <div className="flex flex-col items-center gap-3.5 bg-surface px-6 py-12 text-center">
    <span aria-hidden="true" className="flex size-[38px] items-center justify-center rounded-chip bg-surface-mute text-dim"><Icono className="size-4" /></span>
    <p role="status" className={`m-0 max-w-prose text-[13px] leading-[19px] ${caso === 'cargando' ? 'text-faint' : 'text-ink-soft'}`}>{children}</p>
  </div>
}
