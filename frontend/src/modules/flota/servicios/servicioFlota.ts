import { actualizar, enviar, eliminar, obtener } from './api'

/** Los cuatro valores de FR-033. `sinDocumentacion` no es lo mismo que estar en regla. */
export type EstadoDocumentacionVehiculo =
  | 'enRegla'
  | 'proximaAvencer'
  | 'vencida'
  | 'sinDocumentacion'

/** Estado de un papel, que es otra escala que la del vehículo (`contracts/README.md`). */
export type EstadoDocumento = 'vigente' | 'proximaAvencer' | 'vencida'

/** Los dos valores operativos de FR-012. No hay estado intermedio. */
export type VehiculoEstado = 'disponible' | 'fueraDeServicio'

/** El filtro suma un tercer valor que no es operativo: el de alta (FR-030a). */
export type FiltroEstadoVehiculo = VehiculoEstado | 'dadoDeBaja'

export interface Resumen {
  id: number
  nombre: string
}

export interface DocumentoVehiculo {
  id: number
  vehiculoId: number
  tipo: Resumen
  numero: string
  fechaEmision: string
  fechaVencimiento: string
  estado: EstadoDocumento
  /** `false` = quedó como historial al cargarse una renovación: se ve, pero no cuenta (FR-024). */
  esVigenteDelTipo: boolean
  diasHastaVencimiento: number
  tieneArchivo: boolean
  archivoNombre: string | null
}

/** Fila del listado: exactamente las siete columnas de `contracts/README.md`. */
export interface VehiculoListado {
  id: number
  patente: string
  marca: string
  modelo: string
  tipo: Resumen
  transportista: Resumen
  activo: boolean
  /** El estado **derivado**, no el guardado: una unidad con el seguro vencido figura fuera de servicio (FR-014). */
  estado: VehiculoEstado
  estadoDocumentacion: EstadoDocumentacionVehiculo
}

export interface VehiculoDetalle extends VehiculoListado {
  /**
   * Lo que el operador eligió y el sistema guardó. La ficha lo necesita para poblar el formulario de
   * edición con el valor real y no con el derivado: si no, editar una unidad parada por papeles
   * vencidos le pisaría en silencio el motivo real (FR-014).
   */
  estadoOperativoGuardado: VehiculoEstado
  /** Todos sus documentos, vigentes e históricos. Vacío es un caso válido (FR-033). */
  documentos: DocumentoVehiculo[]
}

export interface VehiculoPeticion {
  patente: string
  marca: string
  modelo: string
  tipoVehiculoId: number
  transportistaId: number
  estadoOperativo: VehiculoEstado
}

/** Página de resultados (FR-032). `total` cuenta las coincidencias, no las de esta página. */
export interface PaginaDe<T> {
  items: T[]
  total: number
  pagina: number
  tamanioPagina: number
}

/**
 * Los cuatro filtros del listado, combinables entre sí (FR-030).
 *
 * `estado` arranca vacío y significa **sólo los activos** (FR-031). El control lo dice
 * explícitamente: un listado que oculta unidades sin avisar se lee como un error de datos (FR-037).
 */
export interface FiltrosFlota {
  transportistaId: number | ''
  tipoVehiculoId: number | ''
  estado: FiltroEstadoVehiculo | ''
  estadoDocumentacion: EstadoDocumentacionVehiculo | ''
}

export const FILTROS_FLOTA_INICIALES: FiltrosFlota = {
  transportistaId: '',
  tipoVehiculoId: '',
  estado: '',
  estadoDocumentacion: '',
}

export function listarFlota(filtros: FiltrosFlota, pagina: number) {
  const parametros = new URLSearchParams()

  if (filtros.transportistaId !== '') {
    parametros.append('transportistaId', String(filtros.transportistaId))
  }
  if (filtros.tipoVehiculoId !== '') {
    parametros.append('tipoVehiculoId', String(filtros.tipoVehiculoId))
  }
  if (filtros.estado !== '') {
    parametros.append('estado', filtros.estado)
  }
  if (filtros.estadoDocumentacion !== '') {
    parametros.append('estadoDocumentacion', filtros.estadoDocumentacion)
  }
  parametros.append('pagina', String(pagina))

  return obtener<PaginaDe<VehiculoListado>>(`/flota/vehiculos?${parametros.toString()}`)
}

export function obtenerVehiculo(id: number) {
  return obtener<VehiculoDetalle>(`/flota/vehiculos/${id}`)
}

export function crearVehiculo(peticion: VehiculoPeticion) {
  return enviar<VehiculoDetalle>('/flota/vehiculos', peticion)
}

export function modificarVehiculo(id: number, peticion: VehiculoPeticion) {
  return actualizar<VehiculoDetalle>(`/flota/vehiculos/${id}`, peticion)
}

/** Baja lógica: la unidad queda inactiva y su documentación se conserva (FR-001, FR-008). */
export function darDeBajaVehiculo(id: number) {
  return eliminar(`/flota/vehiculos/${id}`)
}

/**
 * Reactivación. El cuerpo es opcional: sólo hace falta si el transportista o el tipo de la unidad
 * fueron dados de baja mientras estuvo afuera (FR-008e).
 */
export function reactivarVehiculo(
  id: number,
  reemplazos: { transportistaId?: number; tipoVehiculoId?: number } = {},
) {
  return enviar<void>(`/flota/vehiculos/${id}/reactivacion`, reemplazos)
}

/** Alerta del panel de vencimientos de flota (FR-035). */
export interface AlertaVencimientoFlota {
  vehiculoId: number
  patente: string
  transportista: Resumen
  documento: DocumentoVehiculo
}

export function listarVencimientosDeFlota() {
  return obtener<AlertaVencimientoFlota[]>('/flota/vencimientos')
}
