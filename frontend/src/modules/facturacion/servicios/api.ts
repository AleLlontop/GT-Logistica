import { ErrorHttp, peticion } from '../../../compartido/clienteHttp'
import type { CodigoError, ErrorApi } from '../../../compartido/tipos'

/**
 * Acceso HTTP del módulo de facturación.
 *
 * Se apoya en el cliente compartido, que es el que hace viajar la cookie de sesión y avisa cuando
 * vence.
 *
 * **Las rutas de acá NO llevan el prefijo `/api`**: se lo antepone `peticion`. Escribirlo igual
 * produce `/api/api/facturas`, que es exactamente el defecto que dejó las 19 pantallas del Módulo 3
 * sin funcionar y que ningún test veía, porque los tests mockean `fetch` y no comparan la ruta
 * (`specs/README.md` §Lo que encontraron los recorridos).
 */

/** Códigos de error que devuelve el backend de este módulo (`contracts/facturacion-api.yaml`). */
export const CodigosErrorFacturas = {
  datosInvalidos: 'datos_invalidos',
  noEncontrada: 'no_encontrado',

  // Empresa emisora
  cuitInvalido: 'cuit_invalido',
  emailInvalido: 'email_invalido',
  archivoNoAdmitido: 'archivo_no_admitido',
  archivoNoGuardado: 'archivo_no_guardado',

  // Emisión (400)
  empresaEmisoraIncompleta: 'empresa_emisora_incompleta',
  clienteInexistente: 'cliente_inexistente',
  clienteInactivo: 'cliente_inactivo',
  clienteSinDomicilio: 'cliente_sin_domicilio',
  viajeSinRemito: 'viaje_sin_remito',
  numeroDuplicado: 'numero_duplicado',
  numeroInvalido: 'numero_invalido',
  sinViajesSeleccionados: 'sin_viajes_seleccionados',
  refacturacionSinReemplazada: 'refacturacion_sin_reemplazada',
  originalConReemplazada: 'original_con_reemplazada',
  vencimientoPagoAnterior: 'vencimiento_pago_anterior',
  caeVencimientoAnterior: 'cae_vencimiento_anterior',
  caeRequerido: 'cae_requerido',
  fechaCobroAnterior: 'fecha_cobro_anterior',
  motivoRequerido: 'motivo_requerido',
  rangoDeFechasRequerido: 'rango_de_fechas_requerido',

  // Estado (409)
  viajeYaFacturado: 'viaje_ya_facturado',
  anuladaYaReemplazada: 'anulada_ya_reemplazada',
  transicionNoPermitida: 'transicion_no_permitida',
  facturaAnuladaInmutable: 'factura_anulada_inmutable',
  facturaCobrada: 'factura_cobrada',

  /**
   * Las dos confirmaciones previas de FR-032. **El primer intento no creó nada**: la pantalla abre el
   * diálogo que corresponda según `motivoConfirmacion` y reintenta con `confirmado: true`.
   */
  emisionRequiereConfirmacion: 'emision_requiere_confirmacion',

  /** Módulo 5, FR-055a: rendir exige el remito porque sale impreso en la factura. */
  remitoRequerido: 'remito_requerido',
} as const

/** Qué hay que confirmar antes de emitir (FR-032). */
export type MotivoConfirmacion = 'viajeEnCero' | 'fechaAnteriorAViaje'

/** Por qué un viaje produce un rechazo. */
export type MotivoViajeEnConflicto =
  | 'sinRemito'
  | 'yaFacturado'
  | 'importeEnCero'
  | 'posteriorAlaFactura'

export interface ViajeEnConflicto {
  id: number
  numero: number
  motivo: MotivoViajeEnConflicto
}

export interface FacturaResumen {
  id: number
  numeroComprobante: string
  fecha: string
  estado: EstadoFacturaVisible
}

/**
 * El cuerpo de error del módulo, con los campos opcionales que llevan los rechazos que necesitan
 * explicarse. Viajan **en el cuerpo además de en el mensaje** para que la pantalla no tenga que
 * extraerlos del texto (precedente [004]).
 */
export interface ErrorDeFactura extends ErrorApi {
  faltantes?: string[] | null
  facturaEnConflicto?: FacturaResumen | null
  viajes?: ViajeEnConflicto[] | null
  motivoConfirmacion?: MotivoConfirmacion | null
  fechaCobro?: string | null
}

/** Los cuatro valores del estado visible, en camelCase igual que en el JSON (convención [003]). */
export type EstadoFacturaVisible = 'pendiente' | 'vencida' | 'pagada' | 'anulada'

export type TipoComprobante = 'facturaA' | 'facturaB' | 'facturaC'

export type TipoFacturacion = 'original' | 'refacturacion'

export type CondicionDeVenta = 'contado' | 'cuentaCorriente' | 'tarjeta' | 'cheque'

/** El formato de paginación del sistema desde el Módulo 3 (convención [003]). */
export interface PaginaDe<T> {
  items: T[]
  total: number
  pagina: number
  tamanioPagina: number
}

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

/**
 * Pide un PDF y lo devuelve como `Blob`.
 *
 * **Es un patrón nuevo en este frontend** (research §2): la vista previa y el documento no son JSON,
 * así que no pueden ir por `peticion`, que parsea la respuesta. Va por `fetch` directo con las mismas
 * dos reglas del cliente compartido —credenciales incluidas y el prefijo `/api`— y traduce el error
 * al mismo `ErrorHttp` que el resto del módulo, para que las pantallas manejen un solo tipo.
 */
export async function obtenerPdf(
  ruta: string,
  opciones: { metodo?: 'GET' | 'POST'; cuerpo?: unknown } = {},
): Promise<Blob> {
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
    throw new ErrorHttp(0, {
      codigo: 'sin_conexion',
      mensaje: 'No pudimos conectarnos con el sistema. Revisá tu conexión y volvé a intentar.',
    })
  }

  if (!respuesta.ok) {
    // El rechazo sí viene en JSON: el servidor sólo devuelve `application/pdf` cuando pudo armarlo.
    let detalle: ErrorDeFactura

    try {
      detalle = (await respuesta.json()) as ErrorDeFactura
    } catch {
      detalle = {
        codigo: 'error_inesperado',
        mensaje: 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.',
      }
    }

    throw new ErrorHttp(respuesta.status, detalle)
  }

  return await respuesta.blob()
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
export function esErrorDeFacturas(error: unknown, codigo: CodigoError) {
  return error instanceof ErrorHttp && error.detalle.codigo === codigo
}

/** El cuerpo del error con sus campos del módulo, o `null` si no es un error del backend. */
export function detalleDeError(error: unknown): ErrorDeFactura | null {
  return error instanceof ErrorHttp ? (error.detalle as ErrorDeFactura) : null
}

// ── Cómo se nombra cada cosa en pantalla (`contracts/README.md`) ─────────────────────────────────

export const NOMBRES_DE_ESTADO: Record<EstadoFacturaVisible, string> = {
  pendiente: 'Pendiente',
  vencida: 'Vencida',
  pagada: 'Pagada',
  anulada: 'Anulada',
}

/** Con la minúscula que pide la oración: `Mostrando sólo las facturas vencidas.` */
export const ESTADO_EN_ORACION: Record<EstadoFacturaVisible, string> = {
  pendiente: 'pendientes',
  vencida: 'vencidas',
  pagada: 'pagadas',
  anulada: 'anuladas',
}

export const NOMBRES_DE_TIPO_COMPROBANTE: Record<TipoComprobante, string> = {
  facturaA: 'Factura A',
  facturaB: 'Factura B',
  facturaC: 'Factura C',
}

export const NOMBRES_DE_TIPO_FACTURACION: Record<TipoFacturacion, string> = {
  original: 'Original',
  refacturacion: 'Refacturación',
}

export const NOMBRES_DE_CONDICION_DE_VENTA: Record<CondicionDeVenta, string> = {
  contado: 'Contado',
  cuentaCorriente: 'Cuenta Corriente',
  tarjeta: 'Tarjeta de Débito / Crédito',
  cheque: 'Cheque',
}
