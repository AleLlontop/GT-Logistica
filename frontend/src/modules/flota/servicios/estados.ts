import type {
  EstadoDocumentacionVehiculo,
  EstadoDocumento,
  FiltroEstadoVehiculo,
  VehiculoEstado,
} from './servicioFlota'

/**
 * Los textos de los estados del módulo, que son **escalas distintas** y se llaman distinto a
 * propósito: una describe un papel, la otra una unidad (`contracts/README.md`).
 *
 * El estado nunca se comunica sólo por color: el texto siempre acompaña, porque un semáforo
 * rojo/amarillo/verde deja afuera a quien no distingue esos colores y acá el estado es la
 * información principal de la pantalla (convención [003]).
 */
export const TEXTO_ESTADO_DOCUMENTO: Record<EstadoDocumento, string> = {
  vigente: 'Vigente',
  proximaAvencer: 'Próxima a vencer',
  vencida: 'Vencida',
}

export const TEXTO_ESTADO_DOCUMENTACION: Record<EstadoDocumentacionVehiculo, string> = {
  enRegla: 'En regla',
  proximaAvencer: 'Próxima a vencer',
  vencida: 'Vencida',
  sinDocumentacion: 'Sin documentación',
}

export const TEXTO_ESTADO_VEHICULO: Record<VehiculoEstado, string> = {
  disponible: 'Disponible',
  fueraDeServicio: 'Fuera de servicio',
}

/** El filtro suma "Dado de baja", que no es un estado operativo sino de alta (FR-030a). */
export const TEXTO_FILTRO_ESTADO: Record<FiltroEstadoVehiculo, string> = {
  disponible: 'Disponible',
  fueraDeServicio: 'Fuera de servicio',
  dadoDeBaja: 'Dado de baja',
}

/** Clase para el color. Va **además** del texto, nunca en su lugar. */
export function claseDeEstado(
  estado: EstadoDocumento | EstadoDocumentacionVehiculo | VehiculoEstado,
) {
  return `estado estado--${estado}`
}

/** `Vence en {n} días` o `Venció hace {n} días`, que es lo que el panel muestra (FR-035). */
export function textoDelPlazo(diasHastaVencimiento: number) {
  if (diasHastaVencimiento === 0) return 'Vence hoy'

  return diasHastaVencimiento > 0
    ? `Vence en ${diasHastaVencimiento} día${diasHastaVencimiento === 1 ? '' : 's'}`
    : `Venció hace ${-diasHastaVencimiento} día${diasHastaVencimiento === -1 ? '' : 's'}`
}

// El formateo de fechas es del sistema entero, no de este módulo: vive en `compartido/fechas`, y
// nunca se usa `new Date(iso).toLocaleDateString()` (convención [003]).
export { formatearFecha } from '../../../compartido/fechas'
