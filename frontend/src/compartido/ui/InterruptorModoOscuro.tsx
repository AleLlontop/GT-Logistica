import { useEffect, useState } from 'react'
import { IconoSol, IconoLuna } from './iconos'
import { cn } from './cn'

const CLAVE_STORAGE = 'gt-tema'

/**
 * Slider / Interruptor accesible para alternar entre modo claro y oscuro (QoL).
 *
 * Persiste la elección del usuario en `localStorage` y actualiza la clase `dark` en `<html>`.
 */
export function InterruptorModoOscuro({ className }: { className?: string }) {
  const [esOscuro, setEsOscuro] = useState(() => {
    if (typeof window === 'undefined') return false
    const guardado = localStorage.getItem(CLAVE_STORAGE)
    if (guardado !== null) {
      return guardado === 'dark'
    }
    if (typeof window.matchMedia === 'function') {
      return window.matchMedia('(prefers-color-scheme: dark)').matches
    }
    return false
  })

  useEffect(() => {
    const raiz = document.documentElement
    if (esOscuro) {
      raiz.classList.add('dark')
      localStorage.setItem(CLAVE_STORAGE, 'dark')
    } else {
      raiz.classList.remove('dark')
      localStorage.setItem(CLAVE_STORAGE, 'light')
    }
  }, [esOscuro])

  return (
    <button
      type="button"
      role="switch"
      aria-checked={esOscuro}
      aria-label={esOscuro ? 'Cambiar a modo claro' : 'Cambiar a modo oscuro'}
      title={esOscuro ? 'Cambiar a modo claro' : 'Cambiar a modo oscuro'}
      onClick={() => setEsOscuro((anterior) => !anterior)}
      className={cn(
        'group relative inline-flex h-7 w-13 shrink-0 items-center rounded-pastilla p-0.5',
        'border border-line transition-colors duration-200 ease-gt cursor-pointer select-none',
        esOscuro ? 'bg-surface-mute' : 'bg-surface-soft',
        className,
      )}
    >
      {/* Iconos de fondo fijos para dar contexto visual */}
      <span className="absolute inset-0 flex items-center justify-between px-1.5 text-faint pointer-events-none">
        <IconoSol
          aria-hidden="true"
          className={cn('size-3.5 transition-opacity', esOscuro ? 'opacity-40' : 'opacity-0')}
        />
        <IconoLuna
          aria-hidden="true"
          className={cn('size-3.5 transition-opacity', esOscuro ? 'opacity-0' : 'opacity-40')}
        />
      </span>

      {/* Perilla deslizante con el icono activo */}
      <span
        className={cn(
          'flex size-5.5 items-center justify-center rounded-full bg-white shadow-pill',
          'transition-transform duration-200 ease-gt',
          esOscuro
            ? 'translate-x-6 bg-surface border border-line-strong text-brand'
            : 'translate-x-0 text-amber-500',
        )}
      >
        {esOscuro ? (
          <IconoLuna aria-hidden="true" className="size-3.5" />
        ) : (
          <IconoSol aria-hidden="true" className="size-3.5" />
        )}
      </span>
    </button>
  )
}
