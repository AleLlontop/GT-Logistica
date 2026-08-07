import { formatearInstante } from '../../../compartido/fechas'
import type { CodigoRol, EstadoUsuario } from '../../../compartido/tipos'

export { formatearFecha } from '../../../compartido/fechas'

/** El último acceso vacío se muestra con texto, no como celda en blanco (contracts/README.md). */
export function formatearUltimoAcceso(iso: string | null): string {
  return iso === null ? 'Nunca ingresó' : formatearInstante(iso)
}

export const NOMBRE_DE_ESTADO: Record<EstadoUsuario, string> = {
  activo: 'Activo',
  inactivo: 'Inactivo',
  bloqueado: 'Bloqueado',
}

export const NOMBRE_DE_TIPO_INTEGRANTE: Record<'chofer' | 'empleado', string> = {
  chofer: 'Chofer',
  empleado: 'Empleado',
}

/** Estado de los cuatro filtros del listado (FR-011). */
export interface Filtros {
  username: string
  email: string
  rol: CodigoRol | ''
  estado: EstadoUsuario | ''
}

export const FILTROS_VACIOS: Filtros = { username: '', email: '', rol: '', estado: '' }
