import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { formatearFecha } from '../../../compartido/fechas'
import { Campo } from '../../../compartido/ui/Campo'
import { clasesDeControl } from '../../../compartido/ui/clases'
import { ConsultaTemporal } from '../componentes/ConsultaTemporal'
import { ReferenciaReporte } from '../componentes/ReferenciaReporte'
import { KpiReporte } from '../componentes/KpiReporte'
import { GraficoAccesible } from '../componentes/GraficoAccesible'
import { DistribucionReporte } from '../componentes/DistribucionReporte'
import { DineroAnalisis, TablaAnalisis } from '../componentes/TablaAnalisis'
import { PestanasReporte } from '../componentes/PestanasReporte'
import { useConsultaTemporal } from '../servicios/useConsultaTemporal'
import type { FilaFactura, FiltrosAnalisis } from '../servicios/tiposAnalisis'
import type { AccesoReportes } from './Dashboard'

type Vista = 'facturacion' | 'facturacion/cobros'
type Comunes = Partial<Pick<FiltrosAnalisis['facturacion'], 'desde' | 'hasta' | 'clienteId' | 'numero'>>
const VISTAS = [{ valor: 'facturacion', nombre: 'Documentos' }, { valor: 'facturacion/cobros', nombre: 'Cobros' }] as const
export function FacturacionReporte(acceso: AccesoReportes) {
  const [vista, setVista] = useState<Vista>('facturacion'), [comunes, setComunes] = useState<Comunes>({})
  return <><PestanasReporte nombre="Perspectivas de facturación" opciones={VISTAS} actual={vista} cambiar={setVista} />{vista === 'facturacion' ? <Documentos {...acceso} iniciales={comunes} registrar={setComunes} /> : <Cobros {...acceso} iniciales={comunes} registrar={setComunes} />}</>
}
interface Props extends AccesoReportes { iniciales: Comunes; registrar: (f: Comunes) => void }
function Documentos({ iniciales, registrar, ...acceso }: Props) {
  const c = useConsultaTemporal('facturacion', 'facturacion.consultar', acceso, iniciales)
  useEffect(() => { const { desde, hasta, clienteId, numero } = c.aplicados; registrar({ desde, hasta, clienteId, numero }) }, [c.aplicados, registrar])
  return <>
    <ConsultaTemporal titulo="Facturación — documentos" consulta={c} filtros={<>
      <ReferenciaReporte key={c.reinicios} seleccionId={c.borrador.clienteId} etiqueta="Cliente" tipo="clientesFacturacion" sesionClave={acceso.sesionClave} onCambiar={id => c.setBorrador(a => ({ ...a, clienteId: id }))} />
      <Campo id="numero-facturas-reporte" etiqueta="Número de factura"><input id="numero-facturas-reporte" className={clasesDeControl(false)} value={c.borrador.numero ?? ''} onChange={e => c.setBorrador(a => ({ ...a, numero: e.target.value }))} /></Campo>
      <Campo id="estado-facturas-reporte" etiqueta="Estado documental"><select id="estado-facturas-reporte" className={clasesDeControl(false)} value={c.borrador.estado ?? ''} onChange={e => c.setBorrador(a => ({ ...a, estado: e.target.value as FilaFactura['estado'] || undefined }))}><option value="">Todos salvo anuladas</option><option value="pendiente">Pendiente</option><option value="vencida">Vencida</option><option value="pagada">Pagada</option><option value="anulada">Anulada</option></select></Campo>
    </>}>{d => <>
      <dl className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4"><KpiReporte titulo="Cantidad de facturas" cantidad={d.resumen.cantidad} /><KpiReporte titulo="Facturado del período" importe={d.resumen.facturado} /><KpiReporte titulo="Cobrado vigente de estos documentos" importe={d.resumen.cobradoVigenteDocumental} /><KpiReporte titulo="Pendiente vigente de estos documentos" importe={d.resumen.pendienteVigenteDocumental} /></dl>
      <p>Documentos seleccionados por fecha de factura. La deuda es vigente al consultar, sin restarle los cobros de otro universo. Las anuladas muestran sus importes nominales en el detalle y aportan cero a los totales financieros.</p>
      <GraficoAccesible titulo="Facturación por mes de factura" unidad="ARS" monetario tipo="lineas" datos={d.series.map(m => ({ etiqueta: m.periodo, valores: { facturado: m.facturado, pendiente: m.pendienteVigenteDocumental } }))} series={[{ clave: 'facturado', nombre: 'Facturado' }, { clave: 'pendiente', nombre: 'Pendiente vigente documental' }]} />
      <DistribucionReporte titulo="Facturado por cliente" recurso="facturacion" dimension="cliente" monetario filtros={c.aplicados} sesionClave={acceso.sesionClave} distribucion={d.distribuciones.cliente} />
      <DistribucionReporte titulo="Facturas por estado" recurso="facturacion" dimension="estado" filtros={c.aplicados} sesionClave={acceso.sesionClave} distribucion={d.distribuciones.estado} />
      <TablaAnalisis titulo="Facturas del período documental" pagina={d.detalle} clave={f => f.id} alPaginar={c.paginar} columnas={[
        { titulo: 'Número', valor: f => <Link to={`/facturas/${f.id}`}>{f.numeroComprobante}</Link> }, { titulo: 'Fecha de factura', valor: f => formatearFecha(f.fecha) }, { titulo: 'Cliente histórico', valor: f => f.clienteRazonSocial }, { titulo: 'Estado', valor: f => ({ pendiente: 'Pendiente', vencida: 'Vencida', pagada: 'Pagada', anulada: 'Anulada' })[f.estado] },
        { titulo: 'Neto', monetaria: true, valor: f => <DineroAnalisis valor={f.neto} /> }, { titulo: 'IVA', monetaria: true, valor: f => <DineroAnalisis valor={f.iva} /> }, { titulo: 'Total', monetaria: true, valor: f => <DineroAnalisis valor={f.total} /> }, { titulo: 'Fecha de cobro', valor: f => f.fechaCobro ? formatearFecha(f.fechaCobro) : 'Sin cobro' },
      ]} />
    </>}</ConsultaTemporal></>
}
function Cobros({ iniciales, registrar, ...acceso }: Props) {
  const c = useConsultaTemporal('facturacion/cobros', 'facturacion.consultar', acceso, iniciales)
  useEffect(() => { const { desde, hasta, clienteId, numero } = c.aplicados; registrar({ desde, hasta, clienteId, numero }) }, [c.aplicados, registrar])
  return <>
    <ConsultaTemporal titulo="Facturación — cobros" consulta={c} filtros={<>
      <ReferenciaReporte key={c.reinicios} seleccionId={c.borrador.clienteId} etiqueta="Cliente" tipo="clientesFacturacion" sesionClave={acceso.sesionClave} onCambiar={id => c.setBorrador(a => ({ ...a, clienteId: id }))} />
      <Campo id="numero-cobros-reporte" etiqueta="Número de factura"><input id="numero-cobros-reporte" className={clasesDeControl(false)} value={c.borrador.numero ?? ''} onChange={e => c.setBorrador(a => ({ ...a, numero: e.target.value }))} /></Campo>
    </>}>{d => <>
      <dl className="grid gap-3 sm:grid-cols-2"><KpiReporte titulo="Cantidad de cobros" cantidad={d.resumen.cantidadCobros} /><KpiReporte titulo="Cobrado por fecha efectiva de cobro" importe={d.resumen.importeCobrado} /></dl>
      <p>Incluye cobros de facturas de cualquier período. Cada cobro corresponde al total completo; no hay cobros parciales.</p>
      <GraficoAccesible titulo="Cobros por mes de cobro" unidad="ARS" monetario tipo="lineas" datos={d.series.map(m => ({ etiqueta: m.periodo, valores: { importe: m.importeCobrado } }))} series={[{ clave: 'importe', nombre: 'Cobrado' }]} />
      <DistribucionReporte titulo="Cobrado por cliente" recurso="facturacion/cobros" dimension="cliente" monetario filtros={c.aplicados} sesionClave={acceso.sesionClave} distribucion={d.distribuciones.cliente} />
      <TablaAnalisis titulo="Cobros fechados en el período" pagina={d.detalle} clave={f => f.facturaId} alPaginar={c.paginar} columnas={[
        { titulo: 'Factura', valor: f => <Link to={`/facturas/${f.facturaId}`}>{f.numeroComprobante}</Link> }, { titulo: 'Cliente histórico', valor: f => f.clienteRazonSocial }, { titulo: 'Fecha de factura', valor: f => formatearFecha(f.fechaFactura) }, { titulo: 'Fecha de cobro', valor: f => formatearFecha(f.fechaCobro) }, { titulo: 'Importe cobrado', monetaria: true, valor: f => <DineroAnalisis valor={f.importeCobrado} /> },
      ]} />
    </>}</ConsultaTemporal></>
}
