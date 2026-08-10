import { actualizar, eliminar, enviar, obtener } from '../servicios/api'

/** A qué se aplica el tipo, y por lo tanto en qué módulo se ofrece (Módulo 4, FR-017). */
export type DocumentacionAmbito = 'chofer' | 'vehiculo'

export const TEXTO_AMBITO: Record<DocumentacionAmbito, string> = {
  chofer: 'Chofer',
  vehiculo: 'Vehículo',
}

export interface TipoDocumentacion {
  id: number
  nombre: string
  diasAvisoVencimiento: number
  /** Obligatorio desde el Módulo 4. Los tipos que ya existían quedaron en `chofer` (FR-017c). */
  ambito: DocumentacionAmbito
  activo: boolean
  /**
   * Cuántos documentos lo usan. Es lo que impide su baja (FR-014) y, desde el Módulo 4, también el
   * cambio de ámbito (FR-017d). Suma los de choferes y los de vehículos (FR-017b).
   */
  documentosAsociados: number
}

export interface TipoDocumentacionRequest {
  nombre: string
  diasAvisoVencimiento: number
  ambito: DocumentacionAmbito
}

/**
 * @param ambito Cada módulo pide sólo los tipos de su ámbito: el formulario de documento de vehículo
 * consume `?ambito=vehiculo&soloActivos=true` y no ve los de chofer (Módulo 4, FR-017a). Omitirlo
 * devuelve los dos, que es lo que muestra la pantalla de mantenimiento.
 */
export function listarTipos(soloActivos: boolean = false, ambito?: DocumentacionAmbito) {
  const parametros = new URLSearchParams()

  if (soloActivos) parametros.append('soloActivos', 'true')
  if (ambito !== undefined) parametros.append('ambito', ambito)

  const query = parametros.toString()
  return obtener<TipoDocumentacion[]>(`/tipos-documentacion${query ? `?${query}` : ''}`)
}

export function crearTipo(peticion: TipoDocumentacionRequest) {
  return enviar<TipoDocumentacion>('/tipos-documentacion', peticion)
}

export function modificarTipo(id: number, peticion: TipoDocumentacionRequest) {
  return actualizar<TipoDocumentacion>(`/tipos-documentacion/${id}`, peticion)
}

export function darDeBajaTipo(id: number) {
  return eliminar(`/tipos-documentacion/${id}`)
}
