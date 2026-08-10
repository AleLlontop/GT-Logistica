import { actualizar, eliminar, enviar, obtener } from '../servicios/api'

export interface TipoVehiculo {
  id: number
  nombre: string
  activo: boolean
  /** Vehículos que lo usan, **activos e inactivos**. Es lo que impide su baja (FR-010). */
  cantidadVehiculos: number
}

export interface TipoVehiculoRequest {
  nombre: string
}

/**
 * Leer el catálogo alcanza con `flota.gestionar` —el formulario de vehículo lo consume—, pero el ABM
 * exige `flota.tipos.gestionar`, que sólo tiene el Administrador del sistema (FR-039).
 */
export function listarTiposVehiculo(soloActivos: boolean = false) {
  const query = soloActivos ? '?soloActivos=true' : ''
  return obtener<TipoVehiculo[]>(`/flota/tipos-vehiculo${query}`)
}

export function crearTipoVehiculo(peticion: TipoVehiculoRequest) {
  return enviar<TipoVehiculo>('/flota/tipos-vehiculo', peticion)
}

export function modificarTipoVehiculo(id: number, peticion: TipoVehiculoRequest) {
  return actualizar<TipoVehiculo>(`/flota/tipos-vehiculo/${id}`, peticion)
}

/** Baja lógica. Se rechaza si el tipo tiene vehículos asociados, informando cuántos (FR-010). */
export function darDeBajaTipoVehiculo(id: number) {
  return eliminar(`/flota/tipos-vehiculo/${id}`)
}

/**
 * Alta de un tipo dado de baja: vuelve a ofrecerse al registrar vehículos (FR-009).
 *
 * Recurso aparte y no un campo de `modificarTipoVehiculo`, igual que la reactivación de vehículo:
 * corregir el nombre nunca cambia de paso el estado del tipo.
 */
export function reactivarTipoVehiculo(id: number) {
  return enviar<TipoVehiculo>(`/flota/tipos-vehiculo/${id}/reactivacion`, {})
}
