import { ErrorHttp, peticion } from '../../../compartido/clienteHttp'
import type { CodigoError } from '../../../compartido/tipos'

/**
 * Acceso HTTP del módulo de flota.
 *
 * Se apoya en el cliente compartido —que es el que hace viajar la cookie de sesión y avisa cuando
 * vence— y reutiliza las mismas piezas que el Módulo 3: la carga de un documento con su archivo
 * escaneado y la descarga de ese archivo funcionan igual, sólo cambian las rutas.
 */

/** Códigos de error que devuelve el backend de este módulo (`contracts/flota-api.yaml`). */
export const CodigosErrorFlota = {
  datosInvalidos: 'datos_invalidos',
  noEncontrado: 'no_encontrado',
  patenteDuplicada: 'patente_duplicada',
  patenteDeVehiculoDadoDeBaja: 'patente_de_vehiculo_dado_de_baja',
  patenteInvalida: 'patente_invalida',
  tipoVehiculoInexistente: 'tipo_vehiculo_inexistente',
  transportistaInexistente: 'transportista_inexistente',
  disponibleConDocumentacionVencida: 'disponible_con_documentacion_vencida',
  disponibleSinDocumentacion: 'disponible_sin_documentacion',
  tipoVehiculoEnUso: 'tipo_vehiculo_en_uso',
  nombreDuplicado: 'nombre_duplicado',
  tipoInexistente: 'tipo_inexistente',
  vencimientoAnteriorAEmision: 'vencimiento_anterior_a_emision',
  archivoNoAdmitido: 'archivo_no_admitido',
  archivoNoGuardado: 'archivo_no_guardado',
  transportistaInactivoAlReactivar: 'transportista_inactivo_al_reactivar',
  tipoInactivoAlReactivar: 'tipo_inactivo_al_reactivar',
} as const

/** Restricciones del archivo adjunto, tal como las fija FR-025. */
export const AdjuntoFlota = {
  tiposAceptados: ['application/pdf', 'image/jpeg', 'image/png'],
  tamanioMaximoEnBytes: 10 * 1024 * 1024,
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
 * tampoco se crea ni se modifica y responde `archivo_no_guardado` (FR-029). Por eso la pantalla que
 * llama a esto tiene que conservar lo que la persona ya tipeó, en vez de limpiar el formulario.
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

  // Sin archivo, el documento es válido igual: queda como documentación sin respaldo (FR-016a). Al
  // corregir, no mandarlo significa conservar el que ya tenía.
  if (archivo) {
    cuerpo.append('archivo', archivo)
  }

  return peticion<T>(ruta, { metodo, cuerpo })
}

/**
 * Ruta de descarga del archivo de un documento de la flota.
 *
 * Es un endpoint del backend, no una dirección del volumen: los escaneos nunca se sirven como
 * contenido estático, así que conocer la ruta no alcanza para verlos sin sesión y sin permiso
 * (FR-038, SC-011).
 *
 * **Es la única ruta del módulo que lleva `/api` escrito.** Va directo a un `href` del navegador y no
 * pasa por `peticion`, que es quien antepone el prefijo al resto.
 */
export function rutaDelArchivoDeFlota(documentoId: number) {
  return `/api/flota/documentacion/${documentoId}/archivo`
}

/** `true` si el error del backend trae ese código. Evita comparar strings sueltos en las pantallas. */
export function esErrorDeFlota(error: unknown, codigo: CodigoError) {
  return error instanceof ErrorHttp && error.detalle.codigo === codigo
}
