import { ErrorHttp, peticion } from '../../../compartido/clienteHttp'
import type { AnularEntrada, Banco, BancoReferencia, ChequeResumen, DetalleCheque, DetalleOrden, EstadoCheque,
  FacturaElegible, FiltrosCheques, FiltrosOrdenes, GenerarEntrada, OrdenResumen, Pagina, ProveedorOpcion, ReferenciasPago, SeleccionFacturas } from './tipos'
export const SIN_PERMISO_PAGOS = 'No tenés permiso para realizar esta operación de pagos. Pedile acceso a un administrador.'
export function mensajePago(error: unknown) {
  return error instanceof ErrorHttp && error.estado === 403 ? SIN_PERMISO_PAGOS
    : error instanceof Error ? error.message : 'No pudimos completar la operación. Volvé a intentar.'
}
function consulta(datos: object) {
  return new URLSearchParams(Object.entries(datos).filter(([, v]) => v !== undefined && v !== '').map(([k, v]) => [k, String(v)])).toString()
}
export const referencias = () => peticion<ReferenciasPago>('/pagos/referencias')
export const proveedoresElegibles = (q: string, pagina: number) => peticion<Pagina<ProveedorOpcion>>(`/pagos/referencias/proveedores?${consulta({ q, pagina })}`)
export const facturasElegibles = (proveedorId: number, pagina: number) => peticion<Pagina<FacturaElegible>>(`/pagos/referencias/proveedores/${proveedorId}/facturas?${consulta({ pagina })}`)
export const seleccionarFacturas = (ids: number[]) => peticion<SeleccionFacturas>(`/pagos/referencias/facturas?${consulta({ ids: ids.join(',') })}`)
export const generarOrden = (entrada: GenerarEntrada) => peticion<DetalleOrden>('/pagos/ordenes', { metodo: 'POST', cuerpo: entrada })
export const consultarOrdenes = (filtros: FiltrosOrdenes) => peticion<Pagina<OrdenResumen>>(`/pagos/ordenes?${consulta(filtros)}`)
export const proveedoresHistoricos = (q: string, pagina: number) => peticion<Pagina<ProveedorOpcion>>(`/pagos/ordenes/proveedores?${consulta({ q, pagina })}`)
export const obtenerOrden = (id: number) => peticion<DetalleOrden>(`/pagos/ordenes/${id}`)
export const anularOrden = (id: number, entrada: AnularEntrada) => peticion<DetalleOrden>(`/pagos/ordenes/${id}/anulacion`, { metodo: 'POST', cuerpo: entrada })
export const consultarCheques = (filtros: FiltrosCheques) => peticion<Pagina<ChequeResumen>>(`/pagos/cheques?${consulta(filtros)}`)
export const bancosHistoricos = () => peticion<{ items: BancoReferencia[] }>('/pagos/cheques/bancos')
export const obtenerCheque = (id: number) => peticion<DetalleCheque>(`/pagos/cheques/${id}`)
export const cambiarEstadoCheque = (id: number, estadoAnterior: EstadoCheque, estadoNuevo: EstadoCheque) =>
  peticion<DetalleCheque>(`/pagos/cheques/${id}/estado`, { metodo: 'POST', cuerpo: { estadoAnterior, estadoNuevo } })
export const listarBancos = (pagina: number) => peticion<Pagina<Banco>>(`/pagos/bancos?${consulta({ pagina })}`)
export const obtenerBanco = (id: number) => peticion<Banco>(`/pagos/bancos/${id}`)
export const guardarBanco = (id: number | undefined, nombre: string, version = 0) => peticion<Banco>(id === undefined ? '/pagos/bancos' : `/pagos/bancos/${id}`, { metodo: id === undefined ? 'POST' : 'PUT', cuerpo: id === undefined ? { nombre } : { nombre, version } })
export const activarBanco = (id: number, activo: boolean) => peticion<Banco>(`/pagos/bancos/${id}/${activo ? 'activacion' : 'desactivacion'}`, { metodo: 'PUT', cuerpo: {} })
