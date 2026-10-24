import { formatearPesos } from '../../../compartido/moneda'

type ValorKpi = { importe: string; cantidad?: never; sinPermiso?: false } | { cantidad: number; importe?: never; sinPermiso?: false } | { sinPermiso: true; importe?: never; cantidad?: never }
export function KpiReporte(props: ValorKpi & { titulo: string; ayuda?: string }) {
  const valor = props.sinPermiso ? 'Acceso no disponible' : props.importe !== undefined ? formatearPesos(props.importe) : props.cantidad.toLocaleString('es-AR')
  return <div className="min-w-0 rounded-card border border-line bg-surface p-5 shadow-card">
    <span aria-hidden="true" className="mb-3 block h-1 w-6 rounded-pastilla bg-brand-fill" />
    <dt className="text-sm font-medium text-ink-soft">{props.titulo}</dt>
    <dd className="mt-2 ml-0 text-[21px] font-bold text-ink tabular-nums [overflow-wrap:anywhere]">{valor}</dd>
    {props.ayuda && <dd className="mt-2 ml-0 text-xs text-ink-soft">{props.ayuda}</dd>}
  </div>
}
