import { enviar, obtener } from '../servicios/api'

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
  return obtener<Transportista[]>(`/api/transportistas${query ? `?${query}` : ''}`)
}

export function crearTransportista(peticion: TransportistaRequest) {
  return enviar<Transportista>('/api/transportistas', peticion)
}
