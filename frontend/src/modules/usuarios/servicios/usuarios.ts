import { peticion } from '../../../compartido/clienteHttp'
import type {
  EstadoUsuario,
  CodigoRol,
  RolConPermisos,
  UsuarioDetalle,
  UsuarioListado,
} from '../../../compartido/tipos'

/** Los cuatro filtros del listado, combinables con "y" (FR-011). */
export interface FiltrosUsuarios {
  username?: string
  email?: string
  rol?: CodigoRol | ''
  estado?: EstadoUsuario | ''
}

export function listarUsuarios(filtros: FiltrosUsuarios = {}): Promise<UsuarioListado[]> {
  const parametros = new URLSearchParams()

  for (const [clave, valor] of Object.entries(filtros)) {
    if (valor !== undefined && valor !== '') {
      parametros.set(clave, valor)
    }
  }

  const consulta = parametros.toString()

  return peticion<UsuarioListado[]>(`/usuarios${consulta ? `?${consulta}` : ''}`)
}

export function obtenerUsuario(id: number): Promise<UsuarioDetalle> {
  return peticion<UsuarioDetalle>(`/usuarios/${id}`)
}

/** Datos del alta (FR-001 a FR-005, FR-008). */
export interface AltaUsuario {
  username: string
  email: string
  password: string
  estado: EstadoUsuario
  roles: CodigoRol[]
  /** `null` es válido y habitual: usuario sin persona asociada. */
  personaId: number | null
}

export function crearUsuario(datos: AltaUsuario): Promise<UsuarioDetalle> {
  return peticion<UsuarioDetalle>('/usuarios', { metodo: 'POST', cuerpo: datos })
}

/** Datos de la edición. Sin contraseña, a propósito (FR-014). */
export interface EdicionUsuario {
  username: string
  email: string
  estado: EstadoUsuario
  personaId: number | null
}

export function modificarUsuario(id: number, datos: EdicionUsuario): Promise<UsuarioDetalle> {
  return peticion<UsuarioDetalle>(`/usuarios/${id}`, { metodo: 'PUT', cuerpo: datos })
}

/** Resultado de un restablecimiento. Nunca trae la contraseña generada (FR-009, SC-004). */
export interface ResultadoRestablecimiento {
  /** `false` si el correo no pudo entregarse (FR-021). */
  enviado: boolean
  mensaje: string
}

export function restablecerPassword(id: number): Promise<ResultadoRestablecimiento> {
  return peticion<ResultadoRestablecimiento>(`/usuarios/${id}/restablecer-password`, {
    metodo: 'POST',
  })
}

/**
 * Reemplaza los roles del usuario: quedan exactamente como se envían, ni más ni menos (FR-018).
 */
export function asignarRoles(id: number, roles: CodigoRol[]): Promise<UsuarioDetalle> {
  return peticion<UsuarioDetalle>(`/usuarios/${id}/roles`, { metodo: 'PUT', cuerpo: { roles } })
}

/** Baja lógica: el usuario queda `inactivo` y su registro no se borra (FR-006). */
export function darDeBajaUsuario(id: number): Promise<void> {
  return peticion<void>(`/usuarios/${id}`, { metodo: 'DELETE' })
}

/** Catálogo de roles con sus permisos agrupados por módulo, en modo lectura (FR-010). */
export function listarRoles(): Promise<RolConPermisos[]> {
  return peticion<RolConPermisos[]>('/roles')
}

/** Los cuatro roles del sistema, con su nombre visible. El catálogo es fijo en esta versión. */
export const ROLES_DEL_SISTEMA: ReadonlyArray<{ codigo: CodigoRol; nombre: string }> = [
  { codigo: 'trafico', nombre: 'Tráfico' },
  { codigo: 'administracion', nombre: 'Administración de la empresa' },
  { codigo: 'gerencia', nombre: 'Gerencia' },
  { codigo: 'administrador_sistema', nombre: 'Administrador del sistema' },
]

export const ESTADOS_DE_USUARIO: ReadonlyArray<{ codigo: EstadoUsuario; nombre: string }> = [
  { codigo: 'activo', nombre: 'Activo' },
  { codigo: 'inactivo', nombre: 'Inactivo' },
  { codigo: 'bloqueado', nombre: 'Bloqueado' },
]
