import { ErrorHttp, peticion } from '../../../compartido/clienteHttp'
import type { CodigoError } from '../../../compartido/tipos'

/**
 * Acceso HTTP del módulo de viajes.
 *
 * Se apoya en el cliente compartido, que es el que hace viajar la cookie de sesión y avisa cuando
 * vence.
 *
 * **Las rutas de acá NO llevan el prefijo `/api`**: se lo antepone `peticion`. Escribirlo igual
 * produce `/api/api/viajes`, que es exactamente el defecto que dejó las 19 pantallas del Módulo 3
 * sin funcionar y que ningún test veía, porque los tests mockean `fetch` y no comparan la ruta
 * (`specs/README.md` §Lo que encontraron los recorridos).
 */

/** Códigos de error que devuelve el backend de este módulo (`contracts/viajes-api.yaml`). */
export const CodigosErrorViajes = {
  datosInvalidos: 'datos_invalidos',
  noEncontrado: 'no_encontrado',

  // Padrón de clientes
  cuitInvalido: 'cuit_invalido',
  cuitDuplicado: 'cuit_duplicado',
  cuitDeClienteDadoDeBaja: 'cuit_de_cliente_dado_de_baja',
  emailInvalido: 'email_invalido',
  clienteConViajes: 'cliente_con_viajes',

  // Viajes
  clienteInexistente: 'cliente_inexistente',
  remitoDuplicado: 'remito_duplicado',
  importeNegativo: 'importe_negativo',
  viajeRendidoInmutable: 'viaje_rendido_inmutable',
  viajeAnuladoInmutable: 'viaje_anulado_inmutable',
  transicionNoPermitida: 'transicion_no_permitida',
  faltaAsignacion: 'falta_asignacion',
  unidadDadaDeBaja: 'unidad_dada_de_baja',
  choferOcupado: 'chofer_ocupado',
  vehiculoOcupado: 'vehiculo_ocupado',
  rendicionRequiereConfirmacion: 'rendicion_requiere_confirmacion',
  motivoRequerido: 'motivo_requerido',

  /** Módulo 6, FR-052: el viaje ya está en una factura vigente. */
  viajeFacturadoInmutable: 'viaje_facturado_inmutable',

  /** Módulo 6, FR-055a: rendir exige el remito porque sale impreso en la factura. */
  remitoRequerido: 'remito_requerido',

  rangoDeFechasRequerido: 'rango_de_fechas_requerido',

  // Asignación
  choferInexistente: 'chofer_inexistente',
  vehiculoInexistente: 'vehiculo_inexistente',
  documentacionVencida: 'documentacion_vencida',
  asignacionNoPermitida: 'asignacion_no_permitida',
  fechaBloqueaAsignacion: 'fecha_bloquea_asignacion',
} as const

export function obtener<T>(ruta: string) {
  return peticion<T>(ruta)
}

export function enviar<T>(ruta: string, cuerpo?: unknown) {
  return peticion<T>(ruta, { metodo: 'POST', cuerpo })
}

export function actualizar<T>(ruta: string, cuerpo: unknown) {
  return peticion<T>(ruta, { metodo: 'PUT', cuerpo })
}

export function eliminar(ruta: string) {
  return peticion<void>(ruta, { metodo: 'DELETE' })
}

/** Arma la query descartando los parámetros vacíos, para no mandar `?clienteId=` sin valor. */
export function query(parametros: Record<string, string | number | boolean | null | undefined>) {
  const partes = new URLSearchParams()

  for (const [nombre, valor] of Object.entries(parametros)) {
    if (valor !== null && valor !== undefined && valor !== '') {
      partes.append(nombre, String(valor))
    }
  }

  const texto = partes.toString()

  return texto === '' ? '' : `?${texto}`
}

/** `true` si el error del backend trae ese código. Evita comparar strings sueltos en las pantallas. */
export function esErrorDeViajes(error: unknown, codigo: CodigoError) {
  return error instanceof ErrorHttp && error.detalle.codigo === codigo
}
