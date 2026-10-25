import { useState, type ReactNode } from 'react'
import { Link } from 'react-router-dom'
import { ArrowUpRight, CalendarClock, HandCoins, ReceiptText, Route, Truck, Wallet, type LucideIcon } from 'lucide-react'
import { Boton } from '../../../compartido/ui/Boton'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { EstadoConsultaAnalisis } from '../componentes/EstadoConsultaAnalisis'
import { GraficoAccesible } from '../componentes/GraficoAccesible'
import { KpiReporte } from '../componentes/KpiReporte'
import { ResumenReporte } from '../componentes/ResumenReporte'
import { SelectorPeriodo } from '../componentes/SelectorPeriodo'
import { erroresPeriodo, mesActualArgentina } from '../servicios/periodoAnalisis'
import { consultarDashboard, queryAnalisis } from '../servicios/servicioAnalisis'
import { useFiltrosAnalisis, useSolicitudAnalisis } from '../servicios/useSolicitudAnalisis'
import type { DashboardAnalisis, PeriodoAnalisis } from '../servicios/tiposAnalisis'

export interface AccesoReportes { sesionClave: string | null; permisos: readonly string[] }
export function Dashboard({ sesionClave, permisos }: AccesoReportes) {
  const filtro = useFiltrosAnalisis<PeriodoAnalisis>(mesActualArgentina())
  const [intento, setIntento] = useState(false)
  const solicitud = useSolicitudAnalisis(permisos.includes('reportes.consultar') ? queryAnalisis(filtro.aplicados) : null, sesionClave, () => consultarDashboard(filtro.aplicados))
  return <>
    <EncabezadoDePantalla titulo="Reportes" resumen="Actividad, documentos y flujos del período. Sólo consulta." />
    <form noValidate className="mb-5 flex flex-wrap items-end gap-3 rounded-card border border-line bg-surface p-4" onSubmit={evento => { evento.preventDefault(); setIntento(true); if (!Object.keys(erroresPeriodo(filtro.borrador)).length) filtro.aplicar() }}>
      <SelectorPeriodo valor={filtro.borrador} onCambiar={filtro.setBorrador} mostrarErrores={intento} />
      <Boton variante="secundario" type="submit">Aplicar período</Boton>
      <Boton variante="secundario" onClick={() => { filtro.restablecer(mesActualArgentina()); setIntento(false) }}>Limpiar</Boton>
    </form>
    <EstadoConsultaAnalisis solicitud={solicitud} />
    {solicitud.estado === 'listo' && <ContenidoDashboard datos={solicitud.datos} periodo={filtro.aplicados} />}
  </>
}

function ContenidoDashboard({ datos, periodo }: { datos: DashboardAnalisis; periodo: PeriodoAnalisis }) {
  const s = datos.secciones
  const kpis: ReactNode[] = [], graficos: ReactNode[] = [], accesos: ReactNode[] = []
  function acceso(titulo: string, ruta: string, Icono: LucideIcon, detalle: string, disponible: boolean, actual = false) {
    accesos.push(<li key={titulo} className={`relative flex min-w-0 items-start gap-3 rounded-card border border-line p-4 ${disponible ? 'group bg-surface shadow-card transition-transform duration-200 ease-gt hover:-translate-y-0.5 hover:border-brand-fill hover:bg-surface-soft active:scale-[0.98] has-[a:focus-visible]:outline-3 has-[a:focus-visible]:outline-offset-3 has-[a:focus-visible]:outline-brand' : 'bg-surface-soft'}`}>
      <span aria-hidden="true" className={`grid size-9 shrink-0 place-items-center rounded-chip ${disponible ? 'bg-brand-soft text-brand' : 'bg-surface-mute text-faint'}`}><Icono className="size-4" /></span>
      <div className="min-w-0 flex-1">
        {disponible
          ? <Link to={`${ruta}${actual ? '' : queryAnalisis(periodo)}`} className="text-sm font-bold text-ink after:absolute after:inset-0 after:rounded-card focus-visible:outline-none">{titulo}</Link>
          : <span className="text-sm font-bold text-ink-soft">{titulo}: acceso no disponible</span>}
        <p className="mt-1 text-xs text-ink-soft">{detalle}{actual && <span> — situación actual, sin corte por período</span>}</p>
      </div>
      {disponible && <ArrowUpRight aria-hidden="true" className="size-4 shrink-0 text-muted transition-transform duration-200 ease-gt group-hover:translate-x-0.5 group-hover:-translate-y-0.5 group-hover:text-brand" />}
    </li>)
  }
  function cantidad(titulo: string, valor: number) { kpis.push(<KpiReporte key={titulo} titulo={titulo} cantidad={valor} />) }
  function importe(titulo: string, valor: string) { kpis.push(<KpiReporte key={titulo} titulo={titulo} importe={valor} />) }
  function denegado(titulo: string) { kpis.push(<KpiReporte key={titulo} titulo={titulo} sinPermiso />) }
  if (s.viajes.estado === 'disponible') {
    const d = s.viajes.datos
    cantidad('Viajes del período', d.resumen.cantidad); cantidad('Viajes realizados', d.resumen.realizados); cantidad('Pendientes de rendición', d.resumen.pendientesRendicion); cantidad('Viajes pendientes', d.resumen.pendientes)
    graficos.push(<GraficoAccesible key="viajes" titulo="Viajes por mes de servicio" unidad="viajes" tipo="lineas" datos={d.series.map(m => ({ etiqueta: m.periodo, valores: { cantidad: m.cantidad, realizados: m.realizados, rendicion: m.pendientesRendicion } }))} series={[{ clave: 'cantidad', nombre: 'Total' }, { clave: 'realizados', nombre: 'Realizados' }, { clave: 'rendicion', nombre: 'Pendientes de rendición' }]} />)
  } else denegado('Viajes')
  if (s.facturacion.estado === 'disponible') {
    const d = s.facturacion.datos
    importe('Facturado por fecha de factura', d.documentos.facturado); importe('Pendiente vigente de facturas del período', d.documentos.pendienteVigenteDocumental); importe('Cobros por fecha de cobro', d.cobros.importeCobrado)
    graficos.push(<GraficoAccesible key="facturas" titulo="Facturación por mes de factura" unidad="ARS" monetario tipo="lineas" datos={d.serieDocumentos.map(m => ({ etiqueta: m.periodo, valores: { importe: m.facturado } }))} series={[{ clave: 'importe', nombre: 'Facturado' }]} />,
      <GraficoAccesible key="cobros" titulo="Cobros por mes de cobro" unidad="ARS" monetario tipo="lineas" datos={d.serieCobros.map(m => ({ etiqueta: m.periodo, valores: { importe: m.importeCobrado } }))} series={[{ clave: 'importe', nombre: 'Cobrado' }]} />)
  } else denegado('Facturación y cobros')
  if (s.liquidaciones.estado === 'disponible') {
    const d = s.liquidaciones.datos
    importe('Liquidado por período liquidado', d.documentos.importeLiquidado); importe('Pendiente vigente de liquidaciones del período', d.documentos.pendienteVigenteDocumental); importe('Pagos vigentes por fecha de pago', d.pagos.importePagadoVigente)
    graficos.push(<GraficoAccesible key="liquidaciones" titulo="Importe por mes liquidado completo" unidad="ARS" monetario tipo="lineas" datos={d.serieDocumentos.map(m => ({ etiqueta: m.periodo, valores: { importe: m.importeLiquidado } }))} series={[{ clave: 'importe', nombre: 'Liquidado' }]} />,
      <GraficoAccesible key="pagos" titulo="Pagos vigentes por mes de pago" unidad="ARS" monetario tipo="lineas" datos={d.seriePagos.map(m => ({ etiqueta: m.periodo, valores: { importe: m.importePagadoVigente } }))} series={[{ clave: 'importe', nombre: 'Pagado vigente' }]} />)
  } else denegado('Liquidaciones y pagos')
  if (s.caja.estado === 'disponible') {
    const d = s.caja.datos
    importe('Ingresos de Caja', d.resumen.ingresos); importe('Egresos de Caja', d.resumen.egresos); importe('Movimiento neto de Caja', d.resumen.neto)
    graficos.push(<GraficoAccesible key="caja" titulo="Caja por mes argentino del movimiento" unidad="ARS" monetario tipo="lineas" datos={d.series.map(m => ({ etiqueta: m.periodo, valores: { ingresos: m.ingresos, egresos: m.egresos, neto: m.neto } }))} series={[{ clave: 'ingresos', nombre: 'Ingresos' }, { clave: 'egresos', nombre: 'Egresos' }, { clave: 'neto', nombre: 'Movimiento neto' }]} />)
  } else denegado('Caja')
  if (s.flota.estado === 'disponible') {
    const d = s.flota.datos
    cantidad('Unidades de flota', d.resumen.cantidadUnidades); cantidad('Viajes realizados con vehículo', d.resumen.totalViajesConVehiculo); cantidad('Viajes sin vehículo en el período', d.resumen.sinVehiculoEnPeriodo)
    graficos.push(<GraficoAccesible key="flota" titulo="Ranking de vehículos" unidad="viajes realizados" datos={[...d.ranking.top, ...(d.ranking.otros ? [d.ranking.otros] : [])].map(g => ({ etiqueta: g.nombre, valores: { cantidad: g.cantidad } }))} series={[{ clave: 'cantidad', nombre: 'Viajes' }]} recorte="Hasta 10 unidades y Otros. El detalle completo está en Utilización de flota." />)
  } else denegado('Utilización de flota')
  for (const [clave, titulo] of [['vencimientosChoferes', 'Vencimientos de choferes a hoy'], ['vencimientosFlota', 'Vencimientos de flota a hoy'], ['vencimientosFacturas', 'Vencimientos de facturas a hoy']] as const) {
    const d = s[clave]
    if (d.estado === 'disponible') cantidad(titulo, d.datos.resumen.cantidad); else denegado(titulo)
  }
  acceso('Viajes', '/reportes/viajes', Route, 'Detalle de viajes del período', s.viajes.estado === 'disponible')
  acceso('Facturación y cobros', '/reportes/facturacion', ReceiptText, 'Facturas emitidas y cobros recibidos', s.facturacion.estado === 'disponible')
  acceso('Liquidaciones y pagos', '/reportes/liquidaciones', HandCoins, 'Liquidaciones del período y sus pagos', s.liquidaciones.estado === 'disponible')
  acceso('Caja', '/reportes/caja', Wallet, 'Ingresos y egresos de Caja', s.caja.estado === 'disponible')
  acceso('Utilización de flota', '/reportes/flota', Truck, 'Viajes realizados por unidad', s.flota.estado === 'disponible')
  acceso('Vencimientos', '/reportes/vencimientos', CalendarClock, 'Choferes, flota y facturas', [s.vencimientosChoferes, s.vencimientosFlota, s.vencimientosFacturas].some(x => x.estado === 'disponible'), true)
  return <div className="space-y-6 text-ink">
    <ResumenReporte contexto={datos.contexto} />
    <section aria-label="Indicadores del período"><h2 className="text-xl font-semibold">Indicadores</h2><dl className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-4">{kpis}</dl></section>
    <section aria-label="Gráficos del período"><h2 className="text-xl font-semibold">Gráficos</h2><div className="grid grid-cols-1 gap-4 xl:grid-cols-2">{graficos}</div></section>
    <section aria-label="Accesos a reportes"><h2 className="text-xl font-semibold">Detalle y exportaciones</h2><p className="mt-1 mb-3 max-w-3xl text-sm text-ink-soft">Documentos y flujos tienen universos separados. Los pendientes son vigentes al consultar; Caja muestra movimientos, sin sumar cobros ni pagos como efectivo adicional.</p><ul className="grid grid-cols-1 gap-3 sm:grid-cols-2 xl:grid-cols-3">{accesos}</ul></section>
  </div>
}
