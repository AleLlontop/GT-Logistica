import { actualizar, eliminar, enviar, obtener } from '../servicios/api'

export interface TipoDocumentacion {
  id: number
  nombre: string
  diasAvisoVencimiento: number
  activo: boolean
  /** Cuántos documentos lo usan. Es lo que impide su baja (FR-014). */
  documentosAsociados: number
}

export interface TipoDocumentacionRequest {
  nombre: string
  diasAvisoVencimiento: number
}

export function listarTipos(soloActivos: boolean = false) {
  const query = soloActivos ? '?soloActivos=true' : ''
  return obtener<TipoDocumentacion[]>(`/tipos-documentacion${query}`)
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
