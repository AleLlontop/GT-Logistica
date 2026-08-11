import { actualizar, enviar, obtener, query } from './api'
import type { PaginaDe } from '../clientes/servicioClientes'

/** Los cuatro valores de FR-031, en camelCase igual que en el JSON (convención [003]). */
export type EstadoViaje = 'pendiente' | 'enCurso' | 'rendido' | 'anulado'

/**
 * Un identificador con su nombre y si sigue activo en su padrón.
 *
 * `activo` en `false` se muestra con la palabra `(inactivo)` al lado del nombre, nunca sólo con un
 * color: un viaje conserva y sigue mostrando al chofer, al vehículo y al cliente que se dieron de
 * baja después (FR-008, FR-030, FR-049).
 */
export interface Resumen {
  id: number
  nombre: string
  activo: boolean
}

/** Fila del listado: las diez columnas de FR-040 más las dos señales derivadas. */
export interface ViajeListado {
  id: number
  numero: number
  /** `yyyy-MM-dd`. Se muestra con `formatearFecha`, nunca con `new Date(iso).toLocaleDateString()`. */
  fecha: string
  cliente: Resumen
  origen: string
  destino: string
  chofer: Resumen | null
  vehiculo: Resumen | null
  /** El registrado en el viaje al asignar el chofer, no el actual del chofer (FR-028, SC-010). */
  transportista: Resumen | null
  estado: EstadoViaje
  importe: number
  /** Derivado al leer: más de 5 días corridos en curso. El estado sigue siendo `enCurso` (FR-039). */
  demorado: boolean
  /** Derivado al leer: la fecha del viaje es anterior a hoy (FR-016). */
  esRetroactivo: boolean
  motivoAnulacion: string | null
}

export interface CambioDeEstado {
  /** `null` sólo en la línea del alta: antes del alta no había estado. Se muestra como `Alta`. */
  estadoAnterior: EstadoViaje | null
  estadoNuevo: EstadoViaje
  usuario: string
  /** Instante UTC con la `Z`. Se muestra en hora local con `formatearInstante` (convención [002]). */
  ocurridoEn: string
}

export interface ViajeDetalle extends ViajeListado {
  numeroRemito: string | null
  detalleCarga: string | null
  /** Completo, de la línea más vieja a la más nueva, empezando por el alta (FR-035, FR-045). */
  historial: CambioDeEstado[]
}

/** Una advertencia que **no** frenó la operación: el guardado ya ocurrió (FR-015a). */
export interface Advertencia {
  codigo: string
  mensaje: string
}

/** El sobre de las tres operaciones que pueden advertir: alta, edición y asignación. */
export interface RespuestaViaje {
  viaje: ViajeDetalle
  advertencias: Advertencia[]
}

/**
 * Lo que manda el formulario de viaje.
 *
 * **No lleva número, estado, chofer ni vehículo**: el número lo genera el sistema, el estado tiene
 * sus tres recursos propios y la asignación el suyo (FR-011, FR-019a, FR-034).
 */
export interface ViajePeticion {
  clienteId: number
  fecha: string
  origen: string
  destino: string
  numeroRemito: string | null
  detalleCarga: string | null
  importe: number
}

/**
 * Los cuatro filtros del listado más la búsqueda (FR-041, FR-042).
 *
 * `estado` vacío significa **todos menos los anulados**, no "todos": el control lo dice con todas las
 * letras llamando a esa opción `Todos menos anulados` (FR-044, FR-049).
 */
export interface FiltrosViajes {
  clienteId: number | ''
  transportistaId: number | ''
  estado: EstadoViaje | ''
  desde: string
  hasta: string
  busqueda: string
}

export const FILTROS_VIAJES_INICIALES: FiltrosViajes = {
  clienteId: '',
  transportistaId: '',
  estado: '',
  desde: '',
  hasta: '',
  busqueda: '',
}

export function listarViajes(filtros: FiltrosViajes, pagina: number) {
  return obtener<PaginaDe<ViajeListado>>(
    `/viajes${query({
      clienteId: filtros.clienteId,
      transportistaId: filtros.transportistaId,
      estado: filtros.estado,
      desde: filtros.desde,
      hasta: filtros.hasta,
      busqueda: filtros.busqueda.trim(),
      pagina,
    })}`,
  )
}

export function obtenerViaje(id: number) {
  return obtener<ViajeDetalle>(`/viajes/${id}`)
}

export function crearViaje(peticion: ViajePeticion) {
  return enviar<RespuestaViaje>('/viajes', peticion)
}

export function modificarViaje(id: number, peticion: ViajePeticion) {
  return actualizar<RespuestaViaje>(`/viajes/${id}`, peticion)
}

/**
 * Las dos listas de la pantalla de asignación (FR-021).
 *
 * Cualquiera de las dos puede venir vacía, y es una respuesta legítima: la pantalla informa qué falta
 * cargar y el viaje se queda `pendiente` sin asignar.
 */
export interface Asignables {
  choferes: Asignable[]
  vehiculos: Asignable[]
}

/**
 * Una opción de los desplegables de asignación (FR-021).
 *
 * `observacion` dice por qué la unidad está observada **a la fecha del viaje** —«Seguro vencido el
 * 10/08/2026»— o es `null` si no lo está. La unidad se ofrece igual: el filtro es el estado operativo
 * guardado y no la documentación, que se resuelve al asignar contra esa misma fecha (SC-014).
 */
export interface Asignable {
  id: number
  nombre: string
  observacion: string | null
}

/**
 * @param fecha La del viaje, en `yyyy-MM-dd`. Sin ella el servidor evalúa contra hoy, que sería una
 * observación equivocada para un viaje retroactivo.
 */
export function listarAsignables(fecha?: string) {
  return obtener<Asignables>(
    fecha === undefined ? '/viajes/asignables' : `/viajes/asignables?fecha=${fecha}`,
  )
}

/**
 * Asigna o reasigna las dos unidades. **Los dos identificadores son obligatorios**: no hay asignación
 * parcial (FR-019b).
 */
export function asignarChoferYVehiculo(id: number, choferId: number, vehiculoId: number) {
  return enviar<RespuestaViaje>(`/viajes/${id}/asignacion`, { choferId, vehiculoId })
}

// ── Ciclo de vida: cada transición es un recurso propio, nunca un campo del `PUT` (FR-034) ──────

export function ponerViajeEnCurso(id: number) {
  return enviar<ViajeDetalle>(`/viajes/${id}/en-curso`)
}

/**
 * Rinde el viaje.
 *
 * **Con importe en cero, el primer intento responde `409 rendicion_requiere_confirmacion` sin cambiar
 * nada** (FR-038). La pantalla muestra el diálogo y reintenta con `confirmado: true`.
 *
 * La confirmación vive en el backend, a diferencia de las bajas del sistema, porque este paso no se
 * deshace: un viaje rendido es inmutable para siempre (SC-007a).
 */
export function rendirViaje(id: number, confirmado = false) {
  return enviar<ViajeDetalle>(`/viajes/${id}/rendicion`, { confirmado })
}

/** El motivo es obligatorio (FR-036). */
export function anularViaje(id: number, motivo: string) {
  return enviar<ViajeDetalle>(`/viajes/${id}/anulacion`, { motivo })
}

// ── Totales por período (FR-046) ────────────────────────────────────────────────────────────────

/** Una fila de cualquiera de los dos cuadros. */
export interface TotalDelPeriodo {
  id: number
  /** Razón social del cliente o nombre del transportista. */
  nombre: string
  cantidadViajes: number
  importeTotal: number
}

export interface TotalesDelPeriodo {
  porCliente: TotalDelPeriodo[]
  /** Los viajes sin transportista asignado no aparecen acá: todavía no se sabe quién los va a hacer. */
  porTransportista: TotalDelPeriodo[]
}

/** El rango es **obligatorio**: sin él el backend responde `rango_de_fechas_requerido` (FR-046a). */
export function consultarTotales(desde: string, hasta: string) {
  return obtener<TotalesDelPeriodo>(`/viajes/totales${query({ desde, hasta })}`)
}

/** Cómo se nombra cada estado en pantalla (`contracts/README.md`). */
export const NOMBRES_DE_ESTADO: Record<EstadoViaje, string> = {
  pendiente: 'Pendiente',
  enCurso: 'En curso',
  rendido: 'Rendido',
  anulado: 'Anulado',
}

/** El nombre con `(inactivo)` cuando corresponde. Nunca sólo un color (FR-049). */
export function nombreConEstado(resumen: Resumen | null): string {
  if (resumen === null) {
    return '—'
  }

  return resumen.activo ? resumen.nombre : `${resumen.nombre} (inactivo)`
}
