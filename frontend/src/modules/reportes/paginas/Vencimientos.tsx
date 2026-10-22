import { useState } from 'react'
import { Link } from 'react-router-dom'
import { EncabezadoDePantalla } from '../../../compartido/ui/EncabezadoDePantalla'
import { formatearFecha } from '../../../compartido/fechas'
import { TEXTO_ESTADO_DOCUMENTO as ESTADO_CHOFER, textoDelPlazo } from '../../choferes/servicios/estados'
import { TEXTO_ESTADO_DOCUMENTO as ESTADO_FLOTA } from '../../flota/servicios/estados'
import { consultarAnalisis } from '../servicios/servicioAnalisis'
import { tipoDeReporteAnalisis } from '../servicios/servicioReportes'
import { useSolicitudAnalisis } from '../servicios/useSolicitudAnalisis'
import type { PanelVencimientos, RespuestasAnalisis } from '../servicios/tiposAnalisis'
import { PestanasReporte } from '../componentes/PestanasReporte'
import { EstadoConsultaAnalisis, SinPermisoReporte } from '../componentes/EstadoConsultaAnalisis'
import { GenerarReporte } from '../componentes/GenerarReporte'
import { ResumenReporte } from '../componentes/ResumenReporte'
import { DineroAnalisis, TablaAnalisis } from '../componentes/TablaAnalisis'
import type { AccesoReportes } from './Dashboard'
const PANELES = [
  { valor: 'vencimientos/choferes', nombre: 'Choferes', permiso: 'choferes.vencimientos.consultar' },
  { valor: 'vencimientos/flota', nombre: 'Flota', permiso: 'flota.vencimientos.consultar' },
  { valor: 'vencimientos/facturas', nombre: 'Facturas', permiso: 'facturacion.consultar' },
] as const
export function VencimientosReporte(acceso: AccesoReportes) {
  const disponibles = acceso.permisos.includes('reportes.consultar') ? PANELES.filter(p => acceso.permisos.includes(p.permiso)) : []
  const [elegido, setElegido] = useState<PanelVencimientos | null>(null)
  const panel = disponibles.find(p => p.valor === elegido)?.valor ?? disponibles[0]?.valor
  return <>{panel ? <>
    <PestanasReporte nombre="Panel de vencimientos" opciones={disponibles} actual={panel} cambiar={setElegido} />
    <PanelVencimientosReporte key={panel} {...acceso} recurso={panel} />
  </> : <><EncabezadoDePantalla titulo="Vencimientos" volverA={{ ruta: '/reportes', etiqueta: 'Reportes' }} /><SinPermisoReporte /></>}</>
}
function PanelVencimientosReporte({ recurso, ...acceso }: AccesoReportes & { recurso: PanelVencimientos }) {
  const [pagina, setPagina] = useState(1)
  const solicitud = useSolicitudAnalisis(`${recurso}:${pagina}`, acceso.sesionClave, () => consultarAnalisis(recurso, { pagina, tamanioPagina: 20 }))
  return <>
    <EncabezadoDePantalla titulo={`Vencimientos — ${PANELES.find(p => p.valor === recurso)!.nombre.toLowerCase()}`} volverA={{ ruta: '/reportes', etiqueta: 'Reportes' }} resumen="Situación actual a la fecha de consulta. El período global no se aplica." accionSecundaria={solicitud.estado === 'listo' && <GenerarReporte reporte={tipoDeReporteAnalisis(recurso)} cantidadDeFilas={solicitud.datos.detalle.total} puedeEmitir={acceso.permisos.includes('reportes.emitir')} />} />
    <EstadoConsultaAnalisis solicitud={solicitud} />
    {solicitud.estado === 'listo' && <div className="space-y-4 text-ink"><ResumenReporte contexto={solicitud.datos.contexto} />
      {recurso === 'vencimientos/choferes' && <Choferes acceso={acceso} datos={solicitud.datos as RespuestasAnalisis['vencimientos/choferes']} alPaginar={setPagina} />}
      {recurso === 'vencimientos/flota' && <Flota acceso={acceso} datos={solicitud.datos as RespuestasAnalisis['vencimientos/flota']} alPaginar={setPagina} />}
      {recurso === 'vencimientos/facturas' && <Facturas acceso={acceso} datos={solicitud.datos as RespuestasAnalisis['vencimientos/facturas']} alPaginar={setPagina} />}
    </div>}
  </>
}
// La consulta tipada se entrega a cada tabla sin cambiar reglas ni exponer archivos de documentos.

function Choferes({ acceso, datos, alPaginar }: { acceso: AccesoReportes; datos: RespuestasAnalisis['vencimientos/choferes']; alPaginar: (p: number) => void }) {
  return <TablaAnalisis titulo="Documentos de choferes por urgencia" pagina={datos.detalle} clave={f => f.documento.id} alPaginar={alPaginar} columnas={[
    { titulo: 'Chofer', valor: f => acceso.permisos.includes('choferes.gestionar') ? <Link to={`/choferes/${f.choferId}`}>{f.apellido}, {f.nombre}</Link> : `${f.apellido}, ${f.nombre}` },
    { titulo: 'Transportista', valor: f => f.transportista.nombre }, { titulo: 'Documentación', valor: f => f.documento.tipo.nombre }, { titulo: 'Número', valor: f => f.documento.numero }, { titulo: 'Vencimiento', valor: f => formatearFecha(f.documento.fechaVencimiento) }, { titulo: 'Estado', valor: f => ESTADO_CHOFER[f.documento.estado] }, { titulo: 'Plazo', valor: f => textoDelPlazo(f.documento.diasHastaVencimiento) },
  ]} />
}
function Flota({ acceso, datos, alPaginar }: { acceso: AccesoReportes; datos: RespuestasAnalisis['vencimientos/flota']; alPaginar: (p: number) => void }) {
  return <TablaAnalisis titulo="Documentos de flota por urgencia" pagina={datos.detalle} clave={f => f.documento.id} alPaginar={alPaginar} columnas={[
    { titulo: 'Patente', valor: f => acceso.permisos.includes('flota.gestionar') ? <Link to={`/flota/${f.vehiculoId}`}>{f.patente}</Link> : f.patente },
    { titulo: 'Transportista', valor: f => f.transportista.nombre }, { titulo: 'Documentación', valor: f => f.documento.tipo.nombre }, { titulo: 'Número', valor: f => f.documento.numero }, { titulo: 'Vencimiento', valor: f => formatearFecha(f.documento.fechaVencimiento) }, { titulo: 'Estado', valor: f => ESTADO_FLOTA[f.documento.estado] }, { titulo: 'Plazo', valor: f => textoDelPlazo(f.documento.diasHastaVencimiento) },
  ]} />
}
function Facturas({ acceso, datos, alPaginar }: { acceso: AccesoReportes; datos: RespuestasAnalisis['vencimientos/facturas']; alPaginar: (p: number) => void }) {
  return <><p>Total del conjunto completo: <DineroAnalisis valor={datos.importeTotal} /></p><TablaAnalisis titulo="Facturas por urgencia de vencimiento" pagina={datos.detalle} clave={f => f.id} alPaginar={alPaginar} columnas={[
    { titulo: 'Factura', valor: f => acceso.permisos.includes('facturacion.consultar') ? <Link to={`/facturas/${f.id}`}>{f.numeroComprobante}</Link> : f.numeroComprobante }, { titulo: 'Cliente', valor: f => f.cliente }, { titulo: 'Total', monetaria: true, valor: f => <DineroAnalisis valor={f.total} /> }, { titulo: 'Vencimiento', valor: f => formatearFecha(f.vencimientoPago) }, { titulo: 'Plazo', valor: f => textoDelPlazo(f.dias) },
  ]} /></>
}
