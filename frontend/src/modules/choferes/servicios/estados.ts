import type { EstadoDocumentacionChofer, EstadoDocumento } from './servicioChoferes'

/**
 * Los textos de los dos estados, que son **dos escalas distintas** y se llaman distinto a propósito:
 * una describe un papel, la otra a una persona (`contracts/README.md`).
 *
 * El estado nunca se comunica sólo por color: el texto siempre acompaña, porque un semáforo
 * rojo/amarillo/verde deja afuera a quien no distingue esos colores y acá el estado es la
 * información principal de la pantalla.
 */
export const TEXTO_ESTADO_DOCUMENTO: Record<EstadoDocumento, string> = {
  vigente: 'Al día',
  proximaAvencer: 'Próxima a vencer',
  vencida: 'Vencida',
}

export const TEXTO_ESTADO_CHOFER: Record<EstadoDocumentacionChofer, string> = {
  enRegla: 'En regla',
  proximaAvencer: 'Próxima a vencer',
  vencida: 'Vencida',
  sinDocumentacion: 'Sin documentación',
}

/** Clase para el color. Va **además** del texto, nunca en su lugar. */
export function claseDeEstado(estado: EstadoDocumento | EstadoDocumentacionChofer) {
  return `estado estado--${estado}`
}

/** `Vence en {n} días` o `Venció hace {n} días`, que es lo que acompaña al estado en el panel. */
export function textoDelPlazo(diasHastaVencimiento: number) {
  if (diasHastaVencimiento === 0) return 'Vence hoy'

  return diasHastaVencimiento > 0
    ? `Vence en ${diasHastaVencimiento} día${diasHastaVencimiento === 1 ? '' : 's'}`
    : `Venció hace ${-diasHastaVencimiento} día${diasHastaVencimiento === -1 ? '' : 's'}`
}

// El formateo de fechas es del sistema entero, no de este módulo: vive en `compartido/fechas`.
export { formatearFecha } from '../../../compartido/fechas'
