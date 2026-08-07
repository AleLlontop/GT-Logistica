import { actualizar, eliminar, enviar, obtener } from '../servicios/api'

export interface Transportista {
  id: number
  nombre: string
  cuit: string
  tipo: 'fisica' | 'juridica'
  telefono: string
  email: string
  activo: boolean
  /** Cuántos choferes activos dependen de él. Es lo que impide su baja (FR-010). */
  choferesActivos: number
}

export interface TransportistaRequest {
  nombre: string
  cuit: string
  tipo: 'fisica' | 'juridica'
  telefono: string
  email: string
}

export function listarTransportistas(texto?: string, soloActivos: boolean = false) {
  const parametros = new URLSearchParams()

  if (texto) {
    parametros.append('texto', texto)
  }
  if (soloActivos) {
    parametros.append('soloActivos', 'true')
  }

  const query = parametros.toString()
  return obtener<Transportista[]>(`/transportistas${query ? `?${query}` : ''}`)
}

export function obtenerTransportista(id: number) {
  return obtener<Transportista>(`/transportistas/${id}`)
}

export function crearTransportista(peticion: TransportistaRequest) {
  return enviar<Transportista>('/transportistas', peticion)
}

export function modificarTransportista(id: number, peticion: TransportistaRequest) {
  return actualizar<Transportista>(`/transportistas/${id}`, peticion)
}

/** Baja lógica. Se rechaza si tiene choferes activos, informando cuántos (FR-010). */
export function darDeBajaTransportista(id: number) {
  return eliminar(`/transportistas/${id}`)
}
