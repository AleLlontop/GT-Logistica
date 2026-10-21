import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { formatearFecha } from '../../../compartido/fechas'
import { Campo } from '../../../compartido/ui/Campo'
import { clasesDeControl } from '../../../compartido/ui/clases'
import { NOMBRES_DE_ESTADO } from '../../liquidaciones/servicios/servicioLiquidaciones'
import { ConsultaTemporal } from '../componentes/ConsultaTemporal'
import { ReferenciaReporte } from '../componentes/ReferenciaReporte'
import { KpiReporte } from '../componentes/KpiReporte'
import { GraficoAccesible } from '../componentes/GraficoAccesible'
import { DistribucionReporte } from '../componentes/DistribucionReporte'
import { DineroAnalisis, TablaAnalisis } from '../componentes/TablaAnalisis'
import { PestanasReporte } from '../componentes/PestanasReporte'
import { useConsultaTemporal } from '../servicios/useConsultaTemporal'
import { instanteArgentina } from '../servicios/periodoAnalisis'
import type { FilaLiquidacion, FiltrosAnalisis } from '../servicios/tiposAnalisis'
import type { AccesoReportes } from './Dashboard'
type Comunes = Partial<Pick<FiltrosAnalisis['liquidaciones'], 'desde' | 'hasta' | 'transportistaId'>>
type Vista = 'liquidaciones' | 'liquidaciones/pagos'
const VISTAS = [{ valor: 'liquidaciones', nombre: 'Documentos' }, { valor: 'liquidaciones/pagos', nombre: 'Pagos' }] as const
interface Props extends AccesoReportes { iniciales: Comunes; registrar: (f: Comunes) => void }
export function LiquidacionesReporte(acceso: AccesoReportes) {
  const [vista, setVista] = useState<Vista>('liquidaciones'), [comunes, setComunes] = useState<Comunes>({})
  return <><PestanasReporte nombre="Perspectivas de liquidaciones" opciones={VISTAS} actual={vista} cambiar={setVista} />{vista === 'liquidaciones' ? <Documentos {...acceso} iniciales={comunes} registrar={setComunes} /> : <Pagos {...acceso} iniciales={comunes} registrar={setComunes} />}</>
}
function Documentos({ iniciales, registrar, ...acceso }: Props) {
  const c = useConsultaTemporal('liquidaciones', 'liquidaciones.consultar', acceso, iniciales)
  useEffect(() => { const { desde, hasta, transportistaId } = c.aplicados; registrar({ desde, hasta, transportistaId }) }, [c.aplicados, registrar])
  return <>
    <ConsultaTemporal titulo="Liquidaciones — documentos" consulta={c} filtros={<>
      <ReferenciaReporte key={c.reinicios} seleccionId={c.borrador.transportistaId} etiqueta="Transportista" tipo="transportistasLiquidaciones" sesionClave={acceso.sesionClave} onCambiar={id => c.setBorrador(a => ({ ...a, transportistaId: id }))} />
      <Campo id="estado-liquidaciones-reporte" etiqueta="Estado documental"><select id="estado-liquidaciones-reporte" className={clasesDeControl(false)} value={c.borrador.estado ?? ''} onChange={e => c.setBorrador(a => ({ ...a, estado: e.target.value as FilaLiquidacion['estado'] || undefined }))}><option value="">Todos salvo anuladas</option>{Object.entries(NOMBRES_DE_ESTADO).map(([codigo, nombre]) => <option key={codigo} value={codigo}>{nombre}</option>)}</select></Campo>
    </>}>{d => <>
      <dl className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4"><KpiReporte titulo="Cantidad de liquidaciones" cantidad={d.resumen.cantidad} /><KpiReporte titulo="Importe liquidado" importe={d.resumen.importeLiquidado} /><KpiReporte titulo="Pagado vigente de estos documentos" importe={d.resumen.pagadoVigenteDocumental} /><KpiReporte titulo="Pendiente vigente de estos documentos" importe={d.resumen.pendienteVigenteDocumental} /></dl>
      <p>Se incluyen meses liquidados completos entre Desde y Hasta, sin prorratear: 15/09–10/10 incluye septiembre y octubre. No se usa fecha de generación. Pagos parciales y deuda vigentes al consultar.</p>
      <GraficoAccesible titulo="Liquidaciones por mes liquidado completo" unidad="ARS" monetario tipo="lineas" datos={d.series.map(m => ({ etiqueta: m.periodo, valores: { liquidado: m.importeLiquidado, pendiente: m.pendienteVigenteDocumental } }))} series={[{ clave: 'liquidado', nombre: 'Liquidado' }, { clave: 'pendiente', nombre: 'Pendiente vigente documental' }]} />
      <DistribucionReporte titulo="Liquidado por transportista" recurso="liquidaciones" dimension="transportista" monetario filtros={c.aplicados} sesionClave={acceso.sesionClave} distribucion={d.distribuciones.transportista} />
      <DistribucionReporte titulo="Liquidaciones por estado" recurso="liquidaciones" dimension="estado" filtros={c.aplicados} sesionClave={acceso.sesionClave} distribucion={d.distribuciones.estado} />
      <TablaAnalisis titulo="Liquidaciones del período liquidado" pagina={d.detalle} clave={f => f.id} alPaginar={c.paginar} columnas={[
        { titulo: 'Número LQ', valor: f => <Link to={`/liquidaciones/${f.id}`}>{f.numero}</Link> }, { titulo: 'Período liquidado', valor: f => f.periodo }, { titulo: 'Transportista', valor: f => f.transportista.nombre }, { titulo: 'Estado', valor: f => NOMBRES_DE_ESTADO[f.estado] }, { titulo: 'Total', monetaria: true, valor: f => <DineroAnalisis valor={f.importeTotal} /> }, { titulo: 'Pagado vigente', monetaria: true, valor: f => <DineroAnalisis valor={f.pagadoVigente} /> }, { titulo: 'Pendiente vigente', monetaria: true, valor: f => <DineroAnalisis valor={f.pendienteVigente} /> },
      ]} />
    </>}</ConsultaTemporal></>
}
function Pagos({ iniciales, registrar, ...acceso }: Props) {
  const c = useConsultaTemporal('liquidaciones/pagos', 'liquidaciones.consultar', acceso, iniciales)
  useEffect(() => { const { desde, hasta, transportistaId } = c.aplicados; registrar({ desde, hasta, transportistaId }) }, [c.aplicados, registrar])
  return <>
    <ConsultaTemporal titulo="Liquidaciones — pagos" consulta={c} filtros={<>
      <ReferenciaReporte key={c.reinicios} seleccionId={c.borrador.transportistaId} etiqueta="Transportista" tipo="transportistasLiquidaciones" sesionClave={acceso.sesionClave} onCambiar={id => c.setBorrador(a => ({ ...a, transportistaId: id }))} />
      <Campo id="vigencia-pagos-reporte" etiqueta="Vigencia de pagos"><select id="vigencia-pagos-reporte" className={clasesDeControl(false)} value={c.borrador.vigencia ?? 'todos'} onChange={e => c.setBorrador(a => ({ ...a, vigencia: e.target.value as 'todos' | 'vigentes' | 'revertidos' }))}><option value="todos">Todos</option><option value="vigentes">Vigentes</option><option value="revertidos">Revertidos</option></select></Campo>
    </>}>{d => <>
      <dl className="grid gap-3 sm:grid-cols-2 xl:grid-cols-4"><KpiReporte titulo="Cantidad de pagos vigentes" cantidad={d.resumen.cantidadPagosVigentes} /><KpiReporte titulo="Pagado vigente por fecha de pago" importe={d.resumen.importePagadoVigente} /><KpiReporte titulo="Cantidad de pagos revertidos" cantidad={d.resumen.cantidadRevertidos} /><KpiReporte titulo="Importe revertido histórico (excluido del vigente)" importe={d.resumen.importeRevertidoHistorico} /></dl>
      <p>Fecha de pago por días exactos, incluso de liquidaciones de meses anteriores. Cada OP se cuenta una vez; la OPP es origen, sin sumar su importe nuevamente. Revertidos excluidos del total vigente aunque la reversión sea posterior al rango.</p>
      <GraficoAccesible titulo="Pagos vigentes por mes de pago" unidad="ARS" monetario tipo="lineas" datos={d.series.map(m => ({ etiqueta: m.periodo, valores: { importe: m.importePagadoVigente } }))} series={[{ clave: 'importe', nombre: 'Pagado vigente' }]} />
      <DistribucionReporte titulo="Pagado vigente por transportista" recurso="liquidaciones/pagos" dimension="transportista" monetario filtros={c.aplicados} sesionClave={acceso.sesionClave} distribucion={d.distribuciones.transportista} />
      <TablaAnalisis titulo="OP fechadas en el período de pago" pagina={d.detalle} clave={f => f.id} alPaginar={c.paginar} columnas={[
        { titulo: 'OP', valor: f => f.numero }, { titulo: 'Liquidación', valor: f => <Link to={`/liquidaciones/${f.liquidacionId}`}>{f.liquidacionNumero}</Link> }, { titulo: 'Período liquidado', valor: f => f.periodoLiquidado }, { titulo: 'Transportista', valor: f => f.transportista.nombre }, { titulo: 'Fecha de pago', valor: f => formatearFecha(f.fechaPago) }, { titulo: 'Importe', monetaria: true, valor: f => <DineroAnalisis valor={f.importe} /> }, { titulo: 'Origen', valor: f => f.origen === 'manual' ? 'Manual' : 'OPP' },
        { titulo: 'OPP de origen', valor: f => f.oppId !== null && acceso.permisos.includes('pagos.consultar') ? <Link to={`/pagos/ordenes/${f.oppId}`}>{f.oppNumero}</Link> : f.oppNumero ?? 'No corresponde' }, { titulo: 'Vigencia', valor: f => f.vigente ? 'Vigente' : 'Revertido' }, { titulo: 'Reversión (Argentina)', valor: f => f.revertidaEn ? instanteArgentina(f.revertidaEn) : 'No corresponde' }, { titulo: 'Revertido por', valor: f => f.revertidaPor?.nombre ?? 'No corresponde' },
      ]} />
    </>}</ConsultaTemporal></>
}
