import { peticion } from '../../../compartido/clienteHttp'
import type { Pagina, ProveedorResumen, FacturaProveedorPeticion, DetalleFacturaProveedor, LiquidacionDisponible, FiltrosFacturaProveedor, FacturaProveedorResumen } from './tipos'

export const buscarProveedores = (q: string, soloActivos: boolean, pagina: number) => peticion<Pagina<ProveedorResumen>>(`/proveedores/busqueda?${new URLSearchParams({ q, soloActivos: String(soloActivos), pagina: String(pagina) })}`)
export async function cargarFacturaProveedor(datos: FacturaProveedorPeticion) {
  const form = new FormData()
  for (const [campo, valor] of Object.entries(datos)) {
    if (valor !== undefined) form.append(campo, valor instanceof File ? valor : String(valor))
  }
  return peticion<DetalleFacturaProveedor>('/proveedores/facturas', { metodo: 'POST', cuerpo: form })
}
export const obtenerFacturaProveedor = (id: number) => peticion<DetalleFacturaProveedor>(`/proveedores/facturas/${id}`)
export const consultarLiquidacionesDisponibles = (id: number) => peticion<LiquidacionDisponible[]>(`/proveedores/${id}/liquidaciones-disponibles`)
export const consultarFacturasProveedor = (filtros: FiltrosFacturaProveedor, pagina: number) => {
  const query = new URLSearchParams({ pagina: String(pagina) })
  for (const [campo, valor] of Object.entries(filtros)) if (valor !== undefined && valor !== '') query.set(campo, String(valor))
  return peticion<Pagina<FacturaProveedorResumen>>(`/proveedores/facturas?${query}`)
}
