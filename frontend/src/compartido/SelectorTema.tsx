import { Moon, Sun } from 'lucide-react'
import { useTema } from './tema'

export function SelectorTema() {
  const { tema, cambiar } = useTema()
  const Icono = tema === 'light' ? Moon : Sun
  const etiqueta = tema === 'light' ? 'Activar tema oscuro' : 'Activar tema claro'
  return <button type="button" className="gyt-icon-button" aria-label={etiqueta} title={etiqueta} onClick={cambiar}>
    <Icono aria-hidden="true" className="size-[17px] transition-transform duration-150 ease-gt active:rotate-12" />
  </button>
}
