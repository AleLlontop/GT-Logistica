import { Link } from 'react-router-dom'
import { Campo } from '../../../compartido/ui/Campo'
import { clasesDeControl } from '../../../compartido/ui/clases'
import { ConsultaTemporal } from '../componentes/ConsultaTemporal'
import { ReferenciaReporte } from '../componentes/ReferenciaReporte'
import { KpiReporte } from '../componentes/KpiReporte'
import { GraficoAccesible } from '../componentes/GraficoAccesible'
import { DistribucionReporte } from '../componentes/DistribucionReporte'
import { TablaAnalisis } from '../componentes/TablaAnalisis'
import { useConsultaTemporal } from '../servicios/useConsultaTemporal'
import type { AccesoReportes } from './Dashboard'
export function UtilizacionFlota(acceso: AccesoReportes) {
  const c = useConsultaTemporal('flota', 'reportes.flota.consultar', acceso)
  return <ConsultaTemporal titulo="Utilización de flota" consulta={c} filtros={<>
    {([{ campo: 'vehiculoId', etiqueta: 'Vehículo', tipo: 'vehiculos' }, { campo: 'tipoVehiculoId', etiqueta: 'Tipo actual', tipo: 'tiposVehiculo' }, { campo: 'transportistaId', etiqueta: 'Transportista / propietario actual', tipo: 'transportistasFlota' }] as const).map(r => <ReferenciaReporte key={`${r.campo}:${c.reinicios}`} etiqueta={r.etiqueta} tipo={r.tipo} sesionClave={acceso.sesionClave} onCambiar={id => c.setBorrador(a => ({ ...a, [r.campo]: id }))} />)}
    <Campo id="activo-flota-reporte" etiqueta="Actividad de la unidad"><select id="activo-flota-reporte" className={clasesDeControl(false)} value={c.borrador.activo === undefined ? '' : String(c.borrador.activo)} onChange={e => c.setBorrador(a => ({ ...a, activo: e.target.value === '' ? undefined : e.target.value === 'true' }))}><option value="">Todas</option><option value="true">Activas</option><option value="false">Inactivas</option></select></Campo>
    <Campo id="orden-flota-reporte" etiqueta="Orden del ranking"><select id="orden-flota-reporte" className={clasesDeControl(false)} value={c.borrador.orden ?? 'cantidadDesc'} onChange={e => c.setBorrador(a => ({ ...a, orden: e.target.value as 'cantidadDesc' | 'cantidadAsc' }))}><option value="cantidadDesc">Mayor cantidad primero</option><option value="cantidadAsc">Menor cantidad primero</option></select></Campo>
  </>}>{d => <>
    <dl className="grid gap-3 sm:grid-cols-3"><KpiReporte titulo="Unidades del conjunto" cantidad={d.resumen.cantidadUnidades} /><KpiReporte titulo="Viajes realizados con vehículo" cantidad={d.resumen.totalViajesConVehiculo} /><KpiReporte titulo="Sin vehículo en el período (fuera del filtro de unidades)" cantidad={d.resumen.sinVehiculoEnPeriodo} /></dl>
    <p>Viajes Rendido y Facturado contados una vez por vehículo y fecha del viaje. Incluye unidades sin viajes e inactivas. Tipo, propietario y disponibilidad son actuales. Desempate por patente e identidad.</p>
    <GraficoAccesible titulo="Viajes por vehículo de esta página del ranking" unidad="viajes realizados" datos={d.detalle.items.slice(0, 10).map(f => ({ etiqueta: f.patente, valores: { cantidad: f.cantidadViajes } }))} series={[{ clave: 'cantidad', nombre: 'Viajes' }]} recorte={`Primeras ${Math.min(10, d.detalle.items.length)} unidades de la página ${d.detalle.pagina}. El ranking completo está en la tabla paginada, sin recortar sus totales.`} />
    <DistribucionReporte titulo="Viajes por tipo actual" recurso="flota" dimension="tipo" filtros={c.aplicados} sesionClave={acceso.sesionClave} distribucion={d.distribuciones.tipo} />
    <DistribucionReporte titulo="Viajes por propietario actual" recurso="flota" dimension="transportista" filtros={c.aplicados} sesionClave={acceso.sesionClave} distribucion={d.distribuciones.transportista} />
    <TablaAnalisis titulo="Ranking completo de unidades" pagina={d.detalle} clave={f => f.id} alPaginar={c.paginar} columnas={[
      { titulo: 'Patente', valor: f => acceso.permisos.includes('flota.gestionar') ? <Link to={`/flota/${f.id}`}>{f.patente}</Link> : f.patente }, { titulo: 'Tipo actual', valor: f => f.tipo.nombre }, { titulo: 'Transportista / propietario actual', valor: f => f.transportista.nombre }, { titulo: 'Cantidad de viajes', valor: f => f.cantidadViajes }, { titulo: 'Actividad actual', valor: f => f.activo ? 'Activa' : 'Inactiva' }, { titulo: 'Disponibilidad actual', valor: f => f.disponibilidadActual === 'disponible' ? 'Disponible' : 'Fuera de servicio' }, { titulo: 'Estado operativo guardado actual', valor: f => f.estadoOperativoGuardado === 'disponible' ? 'Disponible' : 'Fuera de servicio' },
    ]} />
  </>}</ConsultaTemporal>
}
