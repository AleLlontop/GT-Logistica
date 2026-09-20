import { ErrorHttp } from './clienteHttp'
import type { ErrorApi } from './tipos'

/**
 * Pedir un archivo al servidor y entregárselo a quien lo pidió.
 *
 * Vivía en `facturacion/servicios/api.ts` como `obtenerPdf`, la primera copia. Los reportes del
 * Módulo 12 son la segunda y la tercera necesidad a la vez —cinco pantallas, dos formatos—, que es
 * exactamente el umbral de la convención [009]: el formato se lleva a `compartido` cuando aparece la
 * tercera necesidad, y la copia del módulo se reemplaza en la misma feature. La prueba de que es un
 * refactor y no un cambio de comportamiento es que la suite de Facturación pasa **sin modificarse**.
 */

const MENSAJE_SIN_CONEXION =
  'No pudimos conectarnos con el sistema. Revisá tu conexión y volvé a intentar.'

const MENSAJE_INESPERADO = 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.'

/** Un archivo del servidor, con el nombre que el backend le puso. */
export interface ArchivoRecibido {
  blob: Blob
  /** Leído del `Content-Disposition`. `null` si el servidor no lo mandó. */
  nombre: string | null
}

/**
 * Pide un archivo y lo devuelve como `Blob`.
 *
 * **Es un patrón distinto del de `peticion`**: la respuesta no es JSON, así que no puede parsearse.
 * Va por `fetch` directo con las mismas dos reglas del cliente compartido —credenciales incluidas y
 * el prefijo `/api`— y traduce el error al mismo `ErrorHttp` que el resto del sistema, para que las
 * pantallas manejen un solo tipo.
 *
 * **El nombre se lee del `Content-Disposition` y no se recompone en TypeScript.** Lo arma el backend
 * una sola vez y viaja armado (convención [009], FR-012): escribirlo de los dos lados serían dos
 * formatos que se pueden separar.
 */
export async function obtenerArchivo(
  ruta: string,
  opciones: { metodo?: 'GET' | 'POST'; cuerpo?: unknown } = {},
): Promise<ArchivoRecibido> {
  const { metodo = 'GET', cuerpo } = opciones

  let respuesta: Response

  try {
    respuesta = await fetch(`/api${ruta}`, {
      method: metodo,
      credentials: 'include',
      headers: cuerpo === undefined ? {} : { 'Content-Type': 'application/json' },
      body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo),
    })
  } catch {
    throw new ErrorHttp(0, { codigo: 'sin_conexion', mensaje: MENSAJE_SIN_CONEXION })
  }

  if (!respuesta.ok) {
    // El rechazo sí viene en JSON: el servidor sólo devuelve el archivo cuando pudo armarlo.
    let detalle: ErrorApi

    try {
      detalle = (await respuesta.json()) as ErrorApi
    } catch {
      detalle = { codigo: 'error_inesperado', mensaje: MENSAJE_INESPERADO }
    }

    throw new ErrorHttp(respuesta.status, detalle)
  }

  return {
    blob: await respuesta.blob(),
    nombre: nombreDeLaCabecera(respuesta.headers.get('Content-Disposition')),
  }
}

/** Una expresión de una línea: el nombre viaja en ASCII, así que el `filename` simple alcanza. */
function nombreDeLaCabecera(cabecera: string | null): string | null {
  return cabecera?.match(/filename="?([^";]+)"?/i)?.[1] ?? null
}

/** Cómo se le entrega el archivo a quien lo pidió. */
export type ModoDeEntrega = 'abrir' | 'descargar'

/** Qué terminó pasando, para que la pantalla lo pueda anunciar. */
export type EntregaRealizada = 'abierto' | 'descargado'

/**
 * Entrega el archivo: lo abre en una pestaña nueva o lo baja.
 *
 * **Con `abrir`, si el navegador bloquea la ventana emergente cae a la descarga** y lo dice en el
 * valor devuelto. No es opcional: `window.open` desde la continuación de una promesa sólo está
 * permitido mientras dure la *activación transitoria* del clic, que son unos cinco segundos, y un
 * reporte grande —justo el que más importa— puede tardar más. Sin la caída, desaparecería sin
 * explicación (research §8).
 *
 * Una descarga programática, en cambio, **no la bloquea ningún navegador**.
 */
export function entregarArchivo(
  archivo: ArchivoRecibido,
  modo: ModoDeEntrega,
  nombrePorDefecto = 'archivo',
): EntregaRealizada {
  const url = URL.createObjectURL(archivo.blob)

  try {
    if (modo === 'abrir' && window.open(url, '_blank', 'noopener') !== null) {
      return 'abierto'
    }

    const enlace = document.createElement('a')
    enlace.href = url
    enlace.download = archivo.nombre ?? nombrePorDefecto
    document.body.appendChild(enlace)
    enlace.click()
    enlace.remove()

    return 'descargado'
  } finally {
    // Se libera en el siguiente turno: con `abrir`, revocarla ya mismo deja la pestaña en blanco.
    setTimeout(() => URL.revokeObjectURL(url), 0)
  }
}
