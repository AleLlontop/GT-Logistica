import { useRef } from 'react'
import { Boton } from '../../../compartido/ui/Boton'
export function PestanasReporte<T extends string>({ nombre, opciones, actual, cambiar }: { nombre: string; opciones: readonly { valor: T; nombre: string }[]; actual: T; cambiar: (valor: T) => void }) {
  const referencias = useRef<(HTMLButtonElement | null)[]>([])
  return <nav aria-label={nombre} className="mb-4 flex flex-wrap gap-2">{opciones.map((o, i) => <Boton variante="secundario" key={o.valor} ref={nodo => { referencias.current[i] = nodo }} aria-pressed={actual === o.valor} onClick={() => cambiar(o.valor)} onKeyDown={e => {
    const siguiente = e.key === 'ArrowRight' ? (i + 1) % opciones.length : e.key === 'ArrowLeft' ? (i - 1 + opciones.length) % opciones.length : e.key === 'Home' ? 0 : e.key === 'End' ? opciones.length - 1 : -1
    if (siguiente >= 0) { e.preventDefault(); referencias.current[siguiente]?.focus(); cambiar(opciones[siguiente].valor) }
  }}>{o.nombre}</Boton>)}</nav>
}
