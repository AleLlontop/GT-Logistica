import type { ReactNode } from 'react'
import { cn } from './cn'
import { IconoAnulado, IconoEnRegla, IconoPendiente, IconoVencido } from './iconos'

type Tono = 'rendido' | 'pendiente' | 'facturado' | 'anulado' | 'danger'

const TONOS: Record<Tono, { caja: string; chip: string; titulo: string; cuerpo: string }> = {
  rendido: {
    caja: 'border-estado-rendido/20 bg-[#F4F7F6]',
    chip: 'bg-estado-rendido-bg text-estado-rendido',
    titulo: 'text-estado-rendido',
    cuerpo: 'text-[#4A5A56]',
  },
  pendiente: {
    caja: 'border-estado-pendiente/20 bg-[#FAF7F1]',
    chip: 'bg-estado-pendiente-bg text-estado-pendiente',
    titulo: 'text-estado-pendiente',
    cuerpo: 'text-[#5C5344]',
  },
  facturado: {
    caja: 'border-estado-facturado/20 bg-[#F4F5FA]',
    chip: 'bg-estado-facturado-bg text-estado-facturado',
    titulo: 'text-estado-facturado',
    cuerpo: 'text-[#4A4E63]',
  },
  anulado: {
    caja: 'border-line-strong bg-estado-anulado-bg',
    chip: 'bg-white text-estado-anulado',
    titulo: 'text-estado-anulado',
    cuerpo: 'text-ink-soft',
  },
  danger: {
    caja: 'border-danger/20 bg-danger-bg',
    chip: 'bg-white text-danger-text',
    titulo: 'text-danger-text',
    cuerpo: 'text-[#6B4A4B]',
  },
}

const ICONOS: Record<Tono, typeof IconoEnRegla> = {
  rendido: IconoEnRegla,
  pendiente: IconoPendiente,
  facturado: IconoEnRegla,
  anulado: IconoAnulado,
  danger: IconoVencido,
}

interface Props {
  tono: Tono
  titulo: string
  /** Qué pasa **y** la salida. Si no hay salida, por qué (FR-058). */
  children: ReactNode
}

/**
 * El aviso de un registro que quedó cerrado para operar (FR-058).
 *
 * Su regla es una sola y no es de estilo: **dice qué pasa y ofrece la salida**. *"Este viaje está
 * rendido, no se edita, no se reasigna y no se anula"* deja a quien opera trabado; *"revertí la
 * rendición desde Totales y el viaje vuelve a Facturado"* le dice qué hacer. Cuando de verdad no hay
 * salida, el texto dice **por qué** no la hay.
 *
 * Aparece en la ficha del viaje rendido y en la de la factura anulada, que son las dos pantallas
 * donde el encabezado se queda sin acción principal.
 */
export function Callout({ tono, titulo, children }: Props) {
  const estilo = TONOS[tono]
  const Icono = ICONOS[tono]

  return (
    <div className={cn('flex items-start gap-3.5 rounded-card border px-[18px] py-4', estilo.caja)}>
      <span
        className={cn(
          'flex size-8 shrink-0 items-center justify-center rounded-chip',
          estilo.chip,
        )}
      >
        <Icono aria-hidden="true" className="size-3.5" />
      </span>

      <div className="flex flex-col gap-1.5">
        <p className={cn('m-0 text-[13.5px] font-bold tracking-tight', estilo.titulo)}>{titulo}</p>
        <div className={cn('text-[13px] leading-5', estilo.cuerpo)}>{children}</div>
      </div>
    </div>
  )
}
