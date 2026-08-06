/**
 * Tipos y valores compartidos entre las pantallas del módulo, fuera de los archivos de componente
 * para que cada uno de ésos exporte sólo su componente (misma separación que el Módulo 2).
 */

/** Los dos filtros del padrón de transportistas, combinables entre sí. */
export interface FiltrosDeTransportistas {
  texto: string
  soloActivos: boolean
}

export const FILTROS_TRANSPORTISTAS_VACIOS: FiltrosDeTransportistas = {
  texto: '',
  soloActivos: false,
}
