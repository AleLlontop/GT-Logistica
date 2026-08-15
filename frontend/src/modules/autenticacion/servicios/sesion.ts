import { peticion } from '../../../compartido/clienteHttp'
import type { OpcionMenu } from '../../../compartido/tipos'

export interface Rol {
  codigo: string
  nombre: string
}

export interface Sesion {
  username: string
  roles: Rol[]
  opcionesMenu: OpcionMenu[]
  /**
   * Códigos de permiso efectivos. La pantalla decide **por permiso y nunca por rol** qué acciones
   * ofrece (convención [004]).
   *
   * Lo trajo el Módulo 5, el primero en el que una misma pantalla se mira con un permiso y se opera
   * con otro: quien tiene sólo `viajes.consultar` ve el listado y la ficha sin ningún botón de
   * escritura (FR-052). Ocultarlos es una cortesía, no la restricción: invocar la acción a mano
   * igual devuelve 403 (SC-012).
   */
  permisos: string[]
}

/** Los permisos que este frontend consulta por nombre. */
export const Permisos = {
  viajesGestionar: 'viajes.gestionar',
  viajesConsultar: 'viajes.consultar',

  /**
   * Módulo 6. Son **tres** y no dos: es el módulo con la autorización más granular del sistema. Se
   * mira con `facturacion.consultar`, se opera con `facturacion.gestionar`, y anular tiene su propio
   * permiso porque devuelve viajes a `rendido` y no se deshace (FR-066, FR-067).
   */
  facturacionGestionar: 'facturacion.gestionar',
  facturacionConsultar: 'facturacion.consultar',
  facturacionAnular: 'facturacion.anular',
} as const

export function tienePermiso(sesion: Sesion | null, codigo: string): boolean {
  return sesion?.permisos.includes(codigo) ?? false
}

/**
 * Inicia sesión. El servidor devuelve la sesión y emite la cookie; acá no se guarda ninguna
 * credencial, ni en memoria ni en el almacenamiento del navegador.
 *
 * `ignorar401` porque en el login un 401 es un resultado esperado —credenciales incorrectas— y no
 * una sesión vencida: no hay que disparar la redirección por sesión expirada.
 */
export function iniciarSesion(username: string, password: string): Promise<Sesion> {
  return peticion<Sesion>('/auth/login', {
    metodo: 'POST',
    cuerpo: { username, password },
    ignorar401: true,
  })
}

/** Consulta la sesión vigente. Devuelve `null` si no hay ninguna. */
export async function obtenerSesion(): Promise<Sesion | null> {
  try {
    return await peticion<Sesion>('/auth/sesion', { ignorar401: true })
  } catch {
    return null
  }
}

/**
 * Cierra la sesión en el servidor.
 *
 * Nunca falla hacia afuera: si la petición no llega, quien la llamó igual tiene que poder limpiar
 * el estado local y llevar al usuario a la pantalla de ingreso. Dejar a alguien adentro del sistema
 * porque el pedido de salida falló sería el peor resultado posible de apretar "Cerrar sesión".
 */
export async function cerrarSesion(): Promise<void> {
  try {
    await peticion<void>('/auth/logout', { metodo: 'POST', ignorar401: true })
  } catch {
    // La cookie de sesión igual vence sola por inactividad, y el estado local se descarta abajo.
  }
}
