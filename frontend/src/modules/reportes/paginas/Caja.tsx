import { Link } from 'react-router-dom'
import { ConsultaTemporal } from '../componentes/ConsultaTemporal'
import { ReferenciaReporte } from '../componentes/ReferenciaReporte'
import { KpiReporte } from '../componentes/KpiReporte'
import { GraficoAccesible } from '../componentes/GraficoAccesible'
import { DistribucionReporte } from '../componentes/DistribucionReporte'
import { DineroAnalisis, TablaAnalisis } from '../componentes/TablaAnalisis'
import { useConsultaTemporal } from '../servicios/useConsultaTemporal'
import { instanteArgentina } from '../servicios/periodoAnalisis'
import type { AccesoReportes } from './Dashboard'
export function CajaReporte(acceso: AccesoReportes) {
  const c = useConsultaTemporal('caja', 'caja.consultar', acceso)
  return <ConsultaTemporal titulo="Análisis de Caja" consulta={c} filtros={<ReferenciaReporte key={c.reinicios} etiqueta="Caja" tipo="cajas" sesionClave={acceso.sesionClave} onCambiar={id => c.setBorrador(a => ({ ...a, cajaId: id }))} />}>{d => <>
    <dl className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4"><KpiReporte titulo="Cantidad de movimientos" cantidad={d.resumen.cantidad} /><KpiReporte titulo="Ingresos" importe={d.resumen.ingresos} /><KpiReporte titulo="Egresos" importe={d.resumen.egresos} /><KpiReporte titulo="Movimiento neto" importe={d.resumen.neto} /></dl>
    <p>Movimientos efectivamente registrados por día argentino. Un egreso y su ingreso compensatorio se muestran por separado en sus fechas. Los cobros y pagos documentales no se agregan como efectivo.</p>
    <GraficoAccesible titulo="Caja por mes argentino del movimiento" unidad="ARS" monetario tipo="lineas" datos={d.series.map(m => ({ etiqueta: m.periodo, valores: { ingresos: m.ingresos, egresos: m.egresos, neto: m.neto } }))} series={[{ clave: 'ingresos', nombre: 'Ingresos' }, { clave: 'egresos', nombre: 'Egresos' }, { clave: 'neto', nombre: 'Movimiento neto' }]} />
    <DistribucionReporte titulo="Movimientos por tipo" recurso="caja" dimension="tipo" monetario filtros={c.aplicados} sesionClave={acceso.sesionClave} distribucion={d.distribuciones.tipo} />
    <TablaAnalisis titulo="Movimientos de Caja del período" pagina={d.detalle} clave={f => f.id} alPaginar={c.paginar} columnas={[
      { titulo: 'Fecha y hora (Argentina)', valor: f => instanteArgentina(f.fecha) }, { titulo: 'Caja', valor: f => <Link to={`/caja/${f.cajaId}`}>{f.cajaNombre}</Link> }, { titulo: 'Tipo', valor: f => f.tipo === 'ingreso' ? 'Ingreso' : 'Egreso' }, { titulo: 'Importe', monetaria: true, valor: f => <DineroAnalisis valor={f.importe} /> }, { titulo: 'Concepto', valor: f => f.concepto }, { titulo: 'Responsable', valor: f => f.responsable.nombre },
      { titulo: 'Referencia', valor: f => f.ordenPagoProveedorId !== null && acceso.permisos.includes('pagos.consultar') ? <Link to={`/pagos/ordenes/${f.ordenPagoProveedorId}`}>{f.referencia}</Link> : f.referencia ?? 'Sin referencia' },
    ]} />
  </>}</ConsultaTemporal>
}
