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
  /**
   * Una acción de segundo nivel, a la izquierda de la principal y en la misma fila: *Generar
   * reporte* (Módulo 12, FR-005).
   *
   * Va en su propio espacio y **no se pasa por `accionPrincipal`**: un slot llamado *acción
   * principal* que recibe una secundaria es exactamente la clase de cosa que la convención [007]
   * señala. Es opcional y aditiva: ninguna de las 42 pantallas deja de compilar ni cambia, y una
   * pantalla de sólo lectura que gana esta acción **sigue sin acción primaria** — exportar no se
   * vuelve primaria por descarte.
   */
  accionSecundaria?: ReactNode
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
 * no en la tabla de rutas a propósito: como toda pantalla lleva encabezado, poner el título del
 * documento en el mismo lugar donde se escribe el de la pantalla garantiza que las 42 lo tengan y
 * que la número 43 no se olvide (FR-071).
 *
 * **El bloque de selectores por descendiente se retiró** (FR-019, FR-021). Estaba estilando las
 * acciones por el **tipo** del botón —`[&_button]`, `[&_a]`, `[&_button[type=submit]]`— en vez de
 * por una variante declarada, y como los botones de las cinco fichas son todos `type="button"`, el
 * resultado era que *Editar*, *Dar de baja* y *Registrar cobro* se dibujaban idénticas: la ficha no
 * tenía acción principal. Es el mismo antipatrón que la convención [007] resolvió en los formularios,
 * sobreviviendo adentro de una primitiva. Ahora cada llamada declara su nivel con `<Boton variante>`,
 * que es obligatoria y no compila sin ella. La firma de la primitiva no cambia.
 *
 * **El *volver* es terciario y va arriba a la izquierda** (FR-022), fuera de la línea donde se decide
 * qué hacer con la pantalla: es una salida, no una alternativa a la acción principal. Y sigue siendo
 * un `<a>` porque navega (FR-023).
 *
 * El título y su bajada quedan **directamente sobre el lienzo**, que es transparente desde FR-011.
 * Por eso van en `ink` e `ink-soft` y no en `muted`: sobre el lienzo, `muted` mide 4,13:1 (FR-007b).
 */
export function EncabezadoDePantalla({
  titulo,
  accionPrincipal,
  accionSecundaria,
  volverA,
  resumen,
  className,
}: Props) {
  useEffect(() => {
    document.title = `${titulo} · ${NOMBRE_DEL_SISTEMA}`
  }, [titulo])

  return (
    <header className={cn('mb-6 flex flex-col gap-4', className)}>
      {volverA !== undefined && (
        <Link
          to={volverA.ruta}
          className="inline-flex w-fit items-center gap-2.5 rounded-pastilla border border-line bg-white/90 py-1.5 pr-[18px] pl-1.5 text-[12.5px] font-semibold tracking-tight text-ink-soft no-underline shadow-pill transition-colors duration-200 ease-gt hover:bg-white"
        >
          <span className="flex size-7 items-center justify-center rounded-pastilla bg-surface-mute">
            <IconoVolver aria-hidden="true" className="size-3 text-muted" />
          </span>
          {volverA.etiqueta}
        </Link>
      )}

      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="min-w-0">
          <h1 className="m-0 text-[34px] leading-[1.15] font-extrabold tracking-[-0.04em] text-ink">
            {titulo}
          </h1>
          {resumen !== undefined && (
            <div className="mt-1.5 text-[13px] text-ink-soft">{resumen}</div>
          )}
        </div>

        {(accionPrincipal !== undefined || accionSecundaria !== undefined) && (
          <div className="flex flex-wrap items-center gap-2.5">
            {accionSecundaria}
            {accionPrincipal}
          </div>
        )}
      </div>
    </header>
  )
}
