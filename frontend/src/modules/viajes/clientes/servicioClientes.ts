import { actualizar, eliminar, enviar, obtener, query } from '../servicios/api'

/** Una página de resultados (convención [003]). `total` cuenta las coincidencias, no las de esta página. */
export interface PaginaDe<T> {
  items: T[]
  total: number
  pagina: number
  tamanioPagina: number
}

export interface Cliente {
  id: number
  razonSocial: string
  cuit: string
  telefono: string
  email: string
  direccion: string | null
  /** `false` es la baja lógica: se muestra atenuado **y** con la palabra `Inactivo` (FR-008, FR-049). */
  activo: boolean
}

/**
 * Lo que manda el formulario. **No lleva `activo`**: dar de baja y dar de alta son recursos propios,
 * así que corregir una razón social no puede reactivar en silencio a alguien dado de baja (FR-007).
 */
export interface ClientePeticion {
  razonSocial: string
  cuit: string
  telefono: string
  email: string
  direccion: string | null
}

export interface FiltrosClientes {
  /** `true` deja sólo los que el formulario de viaje puede ofrecer (FR-008). */
  soloActivos: boolean
  busqueda: string
}

export const FILTROS_CLIENTES_INICIALES: FiltrosClientes = {
  soloActivos: false,
  busqueda: '',
}

export function listarClientes(filtros: FiltrosClientes, pagina: number) {
  return obtener<PaginaDe<Cliente>>(
    `/clientes${query({
      soloActivos: filtros.soloActivos ? true : '',
      busqueda: filtros.busqueda.trim(),
      pagina,
    })}`,
  )
}

export function obtenerCliente(id: number) {
  return obtener<Cliente>(`/clientes/${id}`)
}

export function crearCliente(peticion: ClientePeticion) {
  return enviar<Cliente>('/clientes', peticion)
}

export function modificarCliente(id: number, peticion: ClientePeticion) {
  return actualizar<Cliente>(`/clientes/${id}`, peticion)
}

/** Baja lógica: el registro no se borra nunca y sus viajes históricos quedan intactos (FR-001). */
export function darDeBajaCliente(id: number) {
  return eliminar(`/clientes/${id}`)
}

/** Recurso propio, idempotente y sin confirmación aparte (FR-007, precedente [004]). */
export function darDeAltaCliente(id: number) {
  return enviar<void>(`/clientes/${id}/alta`)
}
