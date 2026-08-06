import { actualizar, eliminar, enviar, obtener } from './api'

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

/** Fila del listado: exactamente las columnas que exige FR-022. */
export interface ChoferListado {
  id: number
  apellido: string
  nombre: string
  dni: string
  transportista: { id: number; nombre: string }
  activo: boolean
  estadoDocumentacion: EstadoDocumentacionChofer
}

/** Página de resultados (FR-030). `total` cuenta las coincidencias, no las de esta página. */
export interface PaginaDe<T> {
  items: T[]
  total: number
  pagina: number
  tamanioPagina: number
}

/**
 * Los cinco filtros del listado, combinables entre sí (FR-022).
 *
 * `estado` arranca en `activo` y no vacío: un listado que oculta choferes sin decirlo se lee como un
 * error de datos.
 */
export interface FiltrosChoferes {
  apellido: string
  dni: string
  transportistaId: number | ''
  estado: 'activo' | 'inactivo'
  estadoDocumentacion: EstadoDocumentacionChofer | ''
}

export const FILTROS_CHOFERES_INICIALES: FiltrosChoferes = {
  apellido: '',
  dni: '',
  transportistaId: '',
  estado: 'activo',
  estadoDocumentacion: '',
}

export function crearChofer(peticion: ChoferPeticion) {
  return enviar<ChoferDetalle>('/api/choferes', peticion)
}

export function listarChoferes(filtros: FiltrosChoferes, pagina: number) {
  const parametros = new URLSearchParams()

  if (filtros.apellido.trim()) parametros.append('apellido', filtros.apellido.trim())
  if (filtros.dni.trim()) parametros.append('dni', filtros.dni.trim())
  if (filtros.transportistaId !== '') {
    parametros.append('transportistaId', String(filtros.transportistaId))
  }
  parametros.append('estado', filtros.estado)
  if (filtros.estadoDocumentacion !== '') {
    parametros.append('estadoDocumentacion', filtros.estadoDocumentacion)
  }
  parametros.append('pagina', String(pagina))

  return obtener<PaginaDe<ChoferListado>>(`/api/choferes?${parametros.toString()}`)
}

export function obtenerChofer(id: number) {
  return obtener<ChoferDetalle>(`/api/choferes/${id}`)
}

export function modificarChofer(id: number, peticion: ChoferPeticion) {
  return actualizar<ChoferDetalle>(`/api/choferes/${id}`, peticion)
}

/** Baja lógica: el chofer queda inactivo y su documentación se conserva (FR-005, FR-005a). */
export function darDeBajaChofer(id: number) {
  return eliminar(`/api/choferes/${id}`)
}

export function reactivarChofer(id: number) {
  return enviar<void>(`/api/choferes/${id}/reactivacion`, {})
}

/** Alerta del panel de vencimientos (FR-021). */
export interface AlertaVencimiento {
  choferId: number
  apellido: string
  nombre: string
  transportista: { id: number; nombre: string }
  documento: Documento
}

export function listarVencimientos() {
  return obtener<AlertaVencimiento[]>('/api/vencimientos')
}
