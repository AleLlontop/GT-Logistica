import { actualizar, eliminar, obtener } from './api'
import { peticion } from '../../../compartido/clienteHttp'

/** El logo cargado, o `null` si no hay ninguno (FR-003, FR-004). */
export interface Logo {
  nombre: string
  /** Endpoint autorizado, nunca una URL pública: el logo se sirve con permiso (Principio V). */
  url: string
}

/**
 * Los datos del emisor tal como los devuelve el backend.
 *
 * `configurada` en `false` **no es un error**: es el punto de partida del sistema recién instalado, y
 * lo que la pantalla muestra es el formulario vacío con el mensaje que dice qué falta (US1 esc. 1).
 */
export interface EmpresaEmisora {
  configurada: boolean
  /** Los obligatorios que faltan, por nombre. Con la fila ausente son los cuatro (FR-006). */
  faltantes: string[]
  razonSocial: string | null
  cuit: string | null
  domicilio: string | null
  condicionIva: string | null
  ingresosBrutos: string | null
  /** `yyyy-MM-dd`. Se muestra con `formatearFecha`, nunca con `new Date(iso)`. */
  inicioActividades: string | null
  puntoDeVenta: string | null
  cbu: string | null
  telefono: string | null
  email: string | null
  logo: Logo | null
}

/**
 * Lo que manda el formulario.
 *
 * **No lleva el logo**: tiene sus recursos propios, así que guardar un teléfono no puede borrarlo en
 * silencio (precedente [004]).
 */
export interface EmpresaEmisoraPeticion {
  razonSocial: string
  cuit: string
  domicilio: string
  condicionIva: string
  ingresosBrutos: string | null
  inicioActividades: string | null
  puntoDeVenta: string | null
  cbu: string | null
  telefono: string | null
  email: string | null
}

const RUTA = '/facturacion/empresa-emisora'

export function obtenerEmpresaEmisora() {
  return obtener<EmpresaEmisora>(RUTA)
}

/** Crea la fila la primera vez y la actualiza siempre después. No hay alta ni baja (FR-001). */
export function guardarEmpresaEmisora(cuerpo: EmpresaEmisoraPeticion) {
  return actualizar<EmpresaEmisora>(RUTA, cuerpo)
}

/**
 * Sube el logo o reemplaza al que había. Va como `multipart/form-data` porque lleva el archivo.
 *
 * El tipo lo decide el servidor **por la firma del archivo** y no por lo que declare el navegador, así
 * que acá no se valida nada: renombrar un PDF a `.png` se rechaza del otro lado (FR-003).
 */
export function subirLogo(archivo: File) {
  const formulario = new FormData()
  formulario.append('archivo', archivo)

  // Va por `peticion` y no por `actualizar` porque el cuerpo es un `FormData`: el cliente compartido
  // lo detecta y deja que el navegador ponga el `Content-Type` con su `boundary`.
  return peticion<EmpresaEmisora>(`${RUTA}/logo`, { metodo: 'PUT', cuerpo: formulario })
}

/** Idempotente y sin confirmación aparte: se puede volver a subir (precedente [004]). */
export function quitarLogo() {
  return eliminar(`${RUTA}/logo`)
}
