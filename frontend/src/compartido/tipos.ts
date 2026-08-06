/** Opción de menú autorizada, tal como la devuelve el servidor (FR-020). */
export interface OpcionMenu {
  codigo: string
  etiqueta: string
  ruta: string
}

/** Cuerpo de toda respuesta de error del backend. */
export interface ErrorApi {
  codigo: CodigoError
  mensaje: string
  /**
   * Cuando el error corresponde a un campo puntual de un formulario, lo identifica para poder
   * marcarlo en rojo en el lugar correcto. Ausente cuando el error no es de un campo.
   */
  campo?: string
}

export type CodigoError =
  // Módulo 1
  | 'datos_incompletos'
  | 'credenciales_invalidas'
  | 'cuenta_no_habilitada'
  | 'demasiados_intentos'
  | 'sesion_expirada'
  | 'sin_permiso'
  | 'error_inesperado'
  | 'sin_conexion'
  // Módulo 2
  | 'datos_invalidos'
  | 'username_duplicado'
  | 'email_duplicado'
  | 'sin_roles'
  | 'persona_ya_vinculada'
  | 'persona_inexistente'
  | 'ultimo_administrador'
  | 'dni_duplicado'
  | 'persona_vinculada'
  | 'password_actual_incorrecta'
  | 'no_encontrado'

/** Estados posibles de una cuenta (FR-005). */
export type EstadoUsuario = 'activo' | 'inactivo' | 'bloqueado'

/** Los cuatro roles del sistema. El catálogo es fijo en esta versión. */
export type CodigoRol = 'trafico' | 'administracion' | 'gerencia' | 'administrador_sistema'

export interface Rol {
  codigo: CodigoRol
  nombre: string
}

/** Chofer o empleado del padrón (FR-026). */
export interface Persona {
  id: number
  nombre: string
  apellido: string
  dni: string
  tipo: 'chofer' | 'empleado'
  telefono: string
  email: string
  fechaNacimiento: string
  activa: boolean
}

/** Fila del listado de usuarios: las seis columnas de FR-011. */
export interface UsuarioListado {
  id: number
  username: string
  email: string
  estado: EstadoUsuario
  roles: Rol[]
  fechaAlta: string
  /** `null` si nunca ingresó: se muestra como "Nunca ingresó". */
  ultimoAcceso: string | null
}

/** Detalle de un usuario. `persona` en `null` es válido y habitual (FR-013). */
export interface UsuarioDetalle extends UsuarioListado {
  persona: Persona | null
}

export interface PermisoResumen {
  codigo: string
  descripcion: string
}

/** Permisos de un rol agrupados por módulo de negocio (FR-010). */
export interface PermisosDeModulo {
  modulo: string
  permisos: PermisoResumen[]
}

export interface RolConPermisos extends Rol {
  /** Puede venir vacío: el rol todavía no habilita nada implementado. */
  permisosPorModulo: PermisosDeModulo[]
}
