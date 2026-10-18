import { ErrorHttp, peticion } from '../../../compartido/clienteHttp'
import type { Pagina, ProveedorResumen, ProveedorDetalle, ProveedorPeticion, TransportistaExterno } from './tipos'

export interface FiltrosProveedor {
  q?: string; razonSocial?: string; nombreComercial?: string; cuit?: string
  tipo?: string; estado?: string; pagina: number
}
export const SIN_PERMISO = 'No tenés permiso para administrar proveedores. Pedile acceso a un administrador.'
export function mensajeProveedor(error: unknown) {
  return error instanceof ErrorHttp && error.estado === 403 ? SIN_PERMISO
    : error instanceof Error ? error.message : 'No pudimos completar la operación. Volvé a intentar.'
}
function consulta(datos: object) {
  return new URLSearchParams(Object.entries(datos).filter(([, v]) => v !== undefined && v !== '').map(([k, v]) => [k, String(v)])).toString()
}
export const listarProveedores = (filtros: FiltrosProveedor) => peticion<Pagina<ProveedorResumen>>(`/proveedores?${consulta(filtros)}`)
export const obtenerProveedor = (id: number) => peticion<ProveedorDetalle>(`/proveedores/${id}`)
export const listarExternos = (q: string, pagina: number) => peticion<Pagina<TransportistaExterno>>(`/proveedores/transportistas-externos?${consulta({ q, pagina })}`)
export const guardarProveedor = (id: number | undefined, datos: ProveedorPeticion) => peticion<ProveedorDetalle>(id === undefined ? '/proveedores' : `/proveedores/${id}`, { metodo: id === undefined ? 'POST' : 'PUT', cuerpo: datos })
export const cambiarEstadoProveedor = (id: number, version: number, activo: boolean) => peticion<ProveedorDetalle>(`/proveedores/${id}/${activo ? 'alta' : 'baja'}`, { metodo: 'POST', cuerpo: { version } })
