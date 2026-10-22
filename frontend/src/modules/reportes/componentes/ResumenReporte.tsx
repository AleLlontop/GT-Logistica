import type { ReactNode } from 'react'
import { instanteArgentina } from '../servicios/periodoAnalisis'
import { formatearFecha } from '../../../compartido/fechas'
import type { ContextoAnalisis, CriterioTemporal } from '../servicios/tiposAnalisis'

const CRITERIOS: Record<CriterioTemporal, string> = {
  fechaViaje: 'Fecha del viaje', fechaFactura: 'Fecha de factura', fechaCobro: 'Fecha efectiva de cobro',
  periodoLiquidado: 'Período liquidado (meses completos)', fechaPago: 'Fecha efectiva de pago',
  fechaMovimientoArgentina: 'Fecha del movimiento en Argentina', situacionActual: 'Situación actual', perspectivasSeparadas: 'Documentos y flujos con criterios separados',
}
export function ResumenReporte({ contexto, children }: { contexto: ContextoAnalisis; children?: ReactNode }) {
  return <aside aria-label="Contexto de la consulta" className="rounded-card border border-line bg-surface p-4 text-sm text-ink-soft">
    <p className="m-0 font-semibold text-ink">{CRITERIOS[contexto.criterioTemporal]}</p>
    <p className="mt-1 mb-0">{contexto.criterioTemporal === 'situacionActual'
      ? `Situación actual al ${formatearFecha(contexto.hoyArgentina)}. No es un corte histórico por el período.`
      : `Desde ${formatearFecha(contexto.desde!)} hasta ${formatearFecha(contexto.hasta!)} inclusive.`}</p>
    {contexto.criterioTemporal === 'periodoLiquidado' && <p className="mt-1 mb-0">Se incluyen completos los meses {contexto.periodoLiquidadoDesde} a {contexto.periodoLiquidadoHasta}, sin prorratear.</p>}
    <p className="mt-1 mb-0">Actualizado el {instanteArgentina(contexto.consultadoEn)} (Argentina). La vigencia es la de esta consulta.</p>
    {children}
  </aside>
}
