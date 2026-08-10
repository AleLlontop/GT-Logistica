import { eliminar, enviarConArchivo } from '../servicios/api'
import type { DocumentoVehiculo } from '../servicios/servicioFlota'

export interface DocumentoVehiculoPeticion {
  documentacionTipoId: number
  numero: string
  fechaEmision: string
  fechaVencimiento: string
}

/**
 * Carga un documento de la unidad con su escaneo opcional.
 *
 * La operación es todo o nada: si el archivo no llega a guardarse, el documento tampoco se crea y el
 * backend responde `archivo_no_guardado` (FR-029). Por eso la pantalla que llama a esto conserva lo
 * que la persona ya tipeó en vez de limpiar el formulario.
 */
export function cargarDocumentoVehiculo(
  vehiculoId: number,
  peticion: DocumentoVehiculoPeticion,
  archivo: File | null,
) {
  return enviarConArchivo<DocumentoVehiculo>(
    `/flota/vehiculos/${vehiculoId}/documentacion`,
    { ...peticion },
    archivo,
  )
}

/**
 * Corrige un documento ya cargado. Si no se elige un archivo nuevo, el adjunto actual se conserva; si
 * se elige uno, el anterior se borra al confirmarse el cambio (FR-026, FR-026a).
 */
export function corregirDocumentoVehiculo(
  documentoId: number,
  peticion: DocumentoVehiculoPeticion,
  archivo: File | null,
) {
  return enviarConArchivo<DocumentoVehiculo>(
    `/flota/documentacion/${documentoId}`,
    { ...peticion },
    archivo,
    'PUT',
  )
}

/** Borrado definitivo: se lleva la fila y su archivo, y no se puede deshacer (FR-027, FR-028). */
export function eliminarDocumentoVehiculo(documentoId: number) {
  return eliminar(`/flota/documentacion/${documentoId}`)
}
