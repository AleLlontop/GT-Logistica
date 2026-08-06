import type { ErrorApi } from './tipos'

/**
 * Cliente HTTP del sistema.
 *
 * Dos reglas que no se negocian (contracts §Reglas transversales del frontend):
 *
 * 1. Todas las peticiones envían las credenciales del navegador, para que la cookie de sesión
 *    viaje. El frontend nunca guarda tokens ni datos de sesión en `localStorage` ni en
 *    `sessionStorage`: la sesión vive en una cookie `HttpOnly` que este código no puede leer.
 * 2. Ante un 401 se avisa a quien esté suscripto para que descarte el estado de sesión y lleve al
 *    usuario a la pantalla de ingreso (FR-015).
 */

const MENSAJE_SIN_CONEXION =
  'No pudimos conectarnos con el sistema. Revisá tu conexión y volvé a intentar.'

export class ErrorHttp extends Error {
  readonly estado: number
  readonly detalle: ErrorApi

  constructor(estado: number, detalle: ErrorApi) {
    super(detalle.mensaje)
    this.name = 'ErrorHttp'
    this.estado = estado
    this.detalle = detalle
  }
}

type ManejadorSesionExpirada = () => void

let alExpirarSesion: ManejadorSesionExpirada | null = null

/** Registra qué hacer cuando el servidor responde 401 en cualquier petición. */
export function registrarManejadorDeSesionExpirada(manejador: ManejadorSesionExpirada) {
  alExpirarSesion = manejador
}

interface OpcionesPeticion {
  metodo?: 'GET' | 'POST' | 'PUT' | 'DELETE'
  /**
   * Se serializa como JSON, salvo que sea un `FormData`: ahí viaja como `multipart/form-data`, que
   * es lo que exige la carga de un documento con su archivo escaneado (Módulo 3).
   */
  cuerpo?: unknown
  /** El login es la única operación donde un 401 es un resultado esperado, no una sesión vencida. */
  ignorar401?: boolean
}

function cabeceras(cuerpo: unknown): HeadersInit {
  // Con `FormData` el `Content-Type` lo pone el navegador, porque tiene que incluir el `boundary`
  // que separa las partes. Fijarlo a mano rompe la subida.
  if (cuerpo === undefined || cuerpo instanceof FormData) {
    return {}
  }

  return { 'Content-Type': 'application/json' }
}

function cuerpoSerializado(cuerpo: unknown): BodyInit | undefined {
  if (cuerpo === undefined) {
    return undefined
  }

  return cuerpo instanceof FormData ? cuerpo : JSON.stringify(cuerpo)
}

export async function peticion<T>(
  ruta: string,
  { metodo = 'GET', cuerpo, ignorar401 = false }: OpcionesPeticion = {},
): Promise<T> {
  let respuesta: Response

  try {
    respuesta = await fetch(`/api${ruta}`, {
      method: metodo,
      // Sin esto la cookie de sesión no viaja y nada funciona.
      credentials: 'include',
      headers: cabeceras(cuerpo),
      body: cuerpoSerializado(cuerpo),
    })
  } catch {
    throw new ErrorHttp(0, { codigo: 'sin_conexion', mensaje: MENSAJE_SIN_CONEXION })
  }

  if (respuesta.status === 401 && !ignorar401) {
    alExpirarSesion?.()
  }

  if (respuesta.status === 204) {
    return undefined as T
  }

  const contenido = await leerCuerpo(respuesta)

  if (!respuesta.ok) {
    throw new ErrorHttp(respuesta.status, contenido as ErrorApi)
  }

  return contenido as T
}

async function leerCuerpo(respuesta: Response): Promise<unknown> {
  try {
    return await respuesta.json()
  } catch {
    return {
      codigo: 'error_inesperado',
      mensaje: 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.',
    } satisfies ErrorApi
  }
}
