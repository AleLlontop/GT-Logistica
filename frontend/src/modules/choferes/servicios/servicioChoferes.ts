import { enviar } from './api'

/** Los cuatro valores de FR-029. `sinDocumentacion` no es lo mismo que estar en regla (FR-028). */
export type EstadoDocumentacionChofer =
  | 'enRegla'
  | 'proximaAvencer'
  | 'vencida'
  | 'sinDocumentacion'

/** Estado de un papel, que es otra escala que la del chofer (contracts/README.md). */
export type EstadoDocumento = 'vigente' | 'proximaAvencer' | 'vencida'

export interface ChoferPeticion {
  nombre: string
  apellido: string
  dni: string
  cuil: string
  fechaNacimiento: string
  telefono: string
  email: string
  transportistaId: number
}

export interface Documento {
  id: number
  tipo: { id: number; nombre: string }
  numero: string
  fechaEmision: string
  fechaVencimiento: string
  estado: EstadoDocumento
  /** `false` = quedó como historial al cargarse una renovación: se ve, pero no cuenta (FR-020a). */
  esVigenteDelTipo: boolean
  diasHastaVencimiento: number
  tieneArchivo: boolean
  archivoNombre: string | null
}

export interface ChoferDetalle {
  id: number
  nombre: string
  apellido: string
  dni: string
  cuil: string
  fechaNacimiento: string
  telefono: string
  email: string
  transportista: {
    id: number
    nombre: string
  }
  activo: boolean
  estadoDocumentacion: EstadoDocumentacionChofer
  personaId: number
  /** Todos sus documentos, vigentes e históricos. Vacío es un caso válido (FR-028). */
  documentos: Documento[]
  /**
   * Sólo viene con sentido en la respuesta del alta: `true` cuando el DNI ya estaba en el padrón y
   * se reutilizó esa persona en vez de crear una nueva (FR-006).
   */
  reutilizoPersona: boolean
}

export function crearChofer(peticion: ChoferPeticion) {
  return enviar<ChoferDetalle>('/api/choferes', peticion)
}
