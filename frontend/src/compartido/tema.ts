import { useSyncExternalStore } from 'react'

export type Tema = 'light' | 'dark'
interface ControlTema {
  get: () => Tema
  set: (tema: Tema) => void
  subscribe: (avisar: () => void) => () => void
}
declare global { interface Window { gytTema?: ControlTema } }

// El controlador se carga sin defer en index.html para resolver el tema antes del primer render.
const suscribir = (avisar: () => void) => window.gytTema?.subscribe(avisar) ?? (() => {})
const leer = (): Tema => window.gytTema?.get() ?? 'light'
export function useTema() {
  const tema = useSyncExternalStore(suscribir, leer, () => 'light' as const)
  return { tema, cambiar: () => window.gytTema?.set(tema === 'light' ? 'dark' : 'light') }
}
