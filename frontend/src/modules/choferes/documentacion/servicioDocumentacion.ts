import { eliminar, enviarConArchivo } from '../servicios/api'
import type { Documento } from '../servicios/servicioChoferes'

export interface DocumentoPeticion {
  documentacionTipoId: number
  numero: string
  fechaEmision: string
  fechaVencimiento: string
}

/**
 * Carga un documento con su escaneo opcional.
 *
 * La operación es todo o nada: si el archivo no llega a guardarse, el documento tampoco se crea y el
 * backend responde `archivo_no_guardado` (FR-015e). Por eso la pantalla que llama a esto conserva lo
 * que la persona ya tipeó en vez de limpiar el formulario.
 */
export function cargarDocumento(
  choferId: number,
  peticion: DocumentoPeticion,
  archivo: File | null,
) {
  return enviarConArchivo<Documento>(
    `/choferes/${choferId}/documentacion`,
    { ...peticion },
    archivo,
  )
}

/**
 * Corrige un documento ya cargado. Si no se elige un archivo nuevo, el adjunto actual se conserva
 * (FR-015b).
 */
export function corregirDocumento(
  documentoId: number,
  peticion: DocumentoPeticion,
  archivo: File | null,
) {
  return enviarConArchivo<Documento>(
    `/documentacion/${documentoId}`,
    { ...peticion },
    archivo,
    'PUT',
  )
}

/** Borrado definitivo: se lleva la fila y su archivo, y no se puede deshacer (FR-015c, FR-015d). */
export function eliminarDocumento(documentoId: number) {
  return eliminar(`/documentacion/${documentoId}`)
}
