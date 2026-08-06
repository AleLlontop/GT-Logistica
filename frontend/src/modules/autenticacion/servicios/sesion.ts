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
