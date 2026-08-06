import { ErrorHttp, peticion } from '../../../compartido/clienteHttp'
import type { CodigoError } from '../../../compartido/tipos'

/**
 * Acceso HTTP del módulo de choferes.
 *
 * Se apoya en el cliente compartido —que es el que hace viajar la cookie de sesión y avisa cuando
 * vence— y agrega sólo lo que este módulo necesita y ningún otro tiene todavía: la carga de un
 * documento con su archivo escaneado, y la descarga de ese archivo.
 */

/** Códigos de error que devuelve el backend de este módulo (`contracts/choferes-api.yaml`). */
export const CodigosError = {
  datosInvalidos: 'datos_invalidos',
  cuitDuplicado: 'cuit_duplicado',
  cuilDuplicado: 'cuil_duplicado',
  dniDuplicado: 'dni_duplicado',
  transportistaInexistente: 'transportista_inexistente',
  transportistaConChoferes: 'transportista_con_choferes',
  menorDeEdad: 'menor_de_edad',
  vencimientoAnteriorAEmision: 'vencimiento_anterior_a_emision',
  tipoDuplicado: 'tipo_duplicado',
  tipoInexistente: 'tipo_inexistente',
  tipoConDocumentos: 'tipo_con_documentos',
  archivoNoAdmitido: 'archivo_no_admitido',
  archivoNoGuardado: 'archivo_no_guardado',
  noEncontrado: 'no_encontrado',
} as const

/** Restricciones del archivo adjunto, tal como las fija FR-015a. */
export const Adjunto = {
  tiposAceptados: ['application/pdf', 'image/jpeg', 'image/png'],
  /** Lo que muestra el formulario antes de que alguien elija un archivo. */
  descripcion: 'PDF, JPG o PNG, hasta 10 MB',
} as const

export function obtener<T>(ruta: string) {
  return peticion<T>(ruta)
}

export function enviar<T>(ruta: string, cuerpo: unknown) {
  return peticion<T>(ruta, { metodo: 'POST', cuerpo })
}

export function actualizar<T>(ruta: string, cuerpo: unknown) {
  return peticion<T>(ruta, { metodo: 'PUT', cuerpo })
}

export function eliminar(ruta: string) {
  return peticion<void>(ruta, { metodo: 'DELETE' })
}

/**
 * Envía un formulario con archivo adjunto opcional.
 *
 * El backend trata la operación como todo o nada: si el archivo no llega a guardarse, el documento
 * tampoco se crea y responde `archivo_no_guardado` (FR-015e). Por eso la pantalla que llama a esto
 * tiene que conservar lo que la persona ya tipeó, en vez de limpiar el formulario.
 */
export function enviarConArchivo<T>(
  ruta: string,
  campos: Record<string, string | number | null | undefined>,
  archivo: File | null,
  metodo: 'POST' | 'PUT' = 'POST',
) {
  const cuerpo = new FormData()

  for (const [nombre, valor] of Object.entries(campos)) {
    if (valor !== null && valor !== undefined && valor !== '') {
      cuerpo.append(nombre, String(valor))
    }
  }

  // Sin archivo, el documento es válido igual: queda como documentación sin respaldo. Al corregir,
  // no mandarlo significa conservar el que ya tenía.
  if (archivo) {
    cuerpo.append('archivo', archivo)
  }

  return peticion<T>(ruta, { metodo, cuerpo })
}

/**
 * Ruta de descarga del archivo de un documento.
 *
 * Es un endpoint del backend, no una dirección del volumen: los escaneos nunca se sirven como
 * contenido estático, así que conocer la ruta no alcanza para verlos sin sesión (FR-024).
 */
export function rutaDelArchivo(documentoId: number) {
  return `/api/documentacion/${documentoId}/archivo`
}

/** `true` si el error del backend trae ese código. Evita comparar strings sueltos en las pantallas. */
export function esError(error: unknown, codigo: CodigoError) {
  return error instanceof ErrorHttp && error.detalle.codigo === codigo
}
