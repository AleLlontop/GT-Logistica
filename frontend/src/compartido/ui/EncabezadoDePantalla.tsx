import { useEffect } from 'react'
import { Link } from 'react-router-dom'
import type { ReactNode } from 'react'
import { cn } from './cn'
import { IconoVolver } from './iconos'

export const NOMBRE_DEL_SISTEMA = 'Sistema Integral de Gestión'

interface Props {
  titulo: string
  /** La acción principal de la pantalla: *Nueva factura*, *Registrar viaje*. */
  accionPrincipal?: ReactNode
  /** Adónde se vuelve, cuando la pantalla es una hoja de otra. */
  volverA?: { ruta: string; etiqueta: string }
  /** Datos que acompañan al título sin ser acciones. */
  resumen?: ReactNode
  className?: string
}

/**
 * El encabezado que llevan **las 42 pantallas** del sistema.
 *
 * Además del título y la acción principal, fija el **título de la pestaña del navegador**. Va acá y
 * no en la tabla de rutas a propósito: como toda pantalla lleva encabezado (FR-016), poner el título
 * del documento en el mismo lugar donde se escribe el de la pantalla garantiza que las 42 lo tengan
 * y que la número 43 no se olvide (research §9).
 *
 * La jerarquía es la que pide FR-016: el título de la pantalla pesa más que las acciones de sesión
 * del encabezado del sistema, y *Cerrar sesión* deja de competir con lo que se vino a hacer.
 */
export function EncabezadoDePantalla({
  titulo,
  accionPrincipal,
  volverA,
  resumen,
  className,
}: Props) {
  useEffect(() => {
    document.title = `${titulo} · ${NOMBRE_DEL_SISTEMA}`
  }, [titulo])

  return (
    <header className={cn('mb-6 flex flex-col gap-2', className)}>
      {volverA !== undefined && (
        <Link
          to={volverA.ruta}
          className="inline-flex w-fit items-center gap-1 text-sm text-acento underline underline-offset-2 hover:text-acento-oscuro"
        >
          <IconoVolver aria-hidden="true" className="size-4" />
          {volverA.etiqueta}
        </Link>
      )}

      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <h1 className="text-2xl font-semibold text-texto">{titulo}</h1>
          {resumen !== undefined && (
            <div className="mt-1 text-sm text-texto-suave">{resumen}</div>
          )}
        </div>

        {accionPrincipal !== undefined && (
          /*
           * Los botones que llegan acá son los `<button>` nativos que las fichas tenían al pie.
           * Se los estila por descendiente en vez de reescribir los cinco archivos: por defecto
           * secundarios, y `submit` —el que guarda— destacado, que es el mismo criterio de
           * `clasesDeFormulario` (FR-028).
           */
          <div
            className={cn(
              'flex flex-wrap items-center gap-2',
              '[&_button]:rounded-chico [&_button]:border [&_button]:border-borde-fuerte',
              '[&_button]:bg-superficie [&_button]:px-3 [&_button]:py-1.5',
              '[&_button]:text-sm [&_button]:font-medium [&_button]:text-texto',
              '[&_button:hover]:bg-superficie-hundida',
              '[&_button[type=submit]]:border-acento [&_button[type=submit]]:bg-acento',
              '[&_button[type=submit]]:text-white',
              '[&_button:disabled]:border-borde [&_button:disabled]:bg-superficie-hundida',
              '[&_button:disabled]:text-texto-tenue [&_button:disabled]:cursor-not-allowed',
              '[&_a]:rounded-chico [&_a]:border [&_a]:border-borde-fuerte [&_a]:bg-superficie',
              '[&_a]:px-3 [&_a]:py-1.5 [&_a]:text-sm [&_a]:font-medium [&_a]:text-texto',
              '[&_a]:no-underline [&_a:hover]:bg-superficie-hundida',
            )}
          >
            {accionPrincipal}
          </div>
        )}
      </div>
    </header>
  )
}
