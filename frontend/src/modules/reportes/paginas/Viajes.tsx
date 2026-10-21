import { Link } from 'react-router-dom'
import { formatearFecha } from '../../../compartido/fechas'
import { Campo } from '../../../compartido/ui/Campo'
import { clasesDeControl } from '../../../compartido/ui/clases'
import { NOMBRES_DE_ESTADO } from '../../viajes/servicios/servicioViajes'
import { ConsultaTemporal } from '../componentes/ConsultaTemporal'
import { useConsultaTemporal } from '../servicios/useConsultaTemporal'
import { ReferenciaReporte } from '../componentes/ReferenciaReporte'
import { KpiReporte } from '../componentes/KpiReporte'
import { GraficoAccesible } from '../componentes/GraficoAccesible'
import { DistribucionReporte } from '../componentes/DistribucionReporte'
import { DineroAnalisis, TablaAnalisis } from '../componentes/TablaAnalisis'
import type { FilaViaje } from '../servicios/tiposAnalisis'
import type { AccesoReportes } from './Dashboard'
export function ViajesReporte(acceso: AccesoReportes) {
  const c = useConsultaTemporal('viajes', 'viajes.consultar', acceso)
  return <ConsultaTemporal titulo="Análisis de viajes" consulta={c} filtros={<>
    {(['clienteId', 'transportistaId'] as const).map(campo => <ReferenciaReporte key={`${campo}:${c.reinicios}`} etiqueta={campo === 'clienteId' ? 'Cliente' : 'Transportista'} tipo={campo === 'clienteId' ? 'clientesViajes' : 'transportistasViajes'} sesionClave={acceso.sesionClave} onCambiar={id => c.setBorrador(a => ({ ...a, [campo]: id }))} />)}
    <Campo id="estado-viajes-reporte" etiqueta="Estado"><select id="estado-viajes-reporte" className={clasesDeControl(false)} value={c.borrador.estado ?? ''} onChange={e => c.setBorrador(a => ({ ...a, estado: e.target.value as FilaViaje['estado'] || undefined }))}><option value="">Todos salvo anulados</option>{Object.entries(NOMBRES_DE_ESTADO).map(([codigo, nombre]) => <option key={codigo} value={codigo}>{nombre}</option>)}</select></Campo>
    <Campo id="busqueda-viajes-reporte" etiqueta="Búsqueda"><input id="busqueda-viajes-reporte" className={clasesDeControl(false)} value={c.borrador.busqueda ?? ''} onChange={e => c.setBorrador(a => ({ ...a, busqueda: e.target.value }))} /></Campo>
  </>}>{d => <>
    <dl className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4"><KpiReporte titulo="Total de viajes" cantidad={d.resumen.cantidad} /><KpiReporte titulo="Realizados" cantidad={d.resumen.realizados} /><KpiReporte titulo="Pendientes de rendición" cantidad={d.resumen.pendientesRendicion} /><KpiReporte titulo="Pendientes" cantidad={d.resumen.pendientes} /><KpiReporte titulo="Anulados" cantidad={d.resumen.anulados} /><KpiReporte titulo="Importe del conjunto" importe={d.resumen.importeTotal} /></dl>
    <p>Realizados: Rendido y Facturado. En curso incluye viajes terminados cuyo remito todavía no llegó; Pendiente se mantiene separado.</p>
    <GraficoAccesible titulo="Viajes por mes de servicio" unidad="viajes" tipo="lineas" datos={d.series.map(m => ({ etiqueta: m.periodo, valores: { cantidad: m.cantidad, realizados: m.realizados, rendicion: m.pendientesRendicion } }))} series={[{ clave: 'cantidad', nombre: 'Total' }, { clave: 'realizados', nombre: 'Realizados' }, { clave: 'rendicion', nombre: 'Pendientes de rendición' }]} />
    <div className="grid gap-4 xl:grid-cols-2">{(['estado', 'cliente', 'transportista'] as const).map(dimension => <DistribucionReporte key={dimension} titulo={`Viajes por ${dimension}`} recurso="viajes" dimension={dimension} filtros={c.aplicados} sesionClave={acceso.sesionClave} distribucion={d.distribuciones[dimension]} />)}</div>
    <TablaAnalisis titulo="Viajes del conjunto filtrado" pagina={d.detalle} clave={f => f.id} alPaginar={c.paginar} columnas={[
      { titulo: 'Número', valor: f => acceso.permisos.includes('viajes.consultar') ? <Link to={`/viajes/${f.id}`}>{f.numero}</Link> : f.numero }, { titulo: 'Fecha', valor: f => formatearFecha(f.fecha) }, { titulo: 'Cliente', valor: f => f.cliente.nombre },
      { titulo: 'Origen', valor: f => f.origen }, { titulo: 'Destino', valor: f => f.destino }, { titulo: 'Chofer', valor: f => f.chofer?.nombre ?? 'Sin asignar' }, { titulo: 'Vehículo', valor: f => f.vehiculo?.nombre ?? 'Sin asignar' }, { titulo: 'Transportista', valor: f => f.transportista?.nombre ?? 'Sin asignar' }, { titulo: 'Estado', valor: f => NOMBRES_DE_ESTADO[f.estado] }, { titulo: 'Importe', monetaria: true, valor: f => <DineroAnalisis valor={f.importe} /> },
    ]} />
  </>}</ConsultaTemporal>
}
