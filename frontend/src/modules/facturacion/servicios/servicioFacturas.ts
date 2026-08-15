import {
  actualizar,
  enviar,
  obtener,
  obtenerPdf,
  query,
  type CondicionDeVenta,
  type EstadoFacturaVisible,
  type FacturaResumen,
  type PaginaDe,
  type TipoComprobante,
  type TipoFacturacion,
} from './api'

// ── Armado ──────────────────────────────────────────────────────────────────────────────────────

/**
 * Un viaje que se ofrece para incluir en la factura (FR-019, FR-019a).
 *
 * `puedeFacturarse` en `false` **no lo esconde**: la fila se muestra igual, con la casilla
 * deshabilitada y la leyenda que lo explica. Un listado no oculta filas en silencio ni las ofrece sin
 * decir lo que sabe de ellas (convención [003]).
 */
export interface ViajeFacturable {
  id: number
  numero: number
  /** `yyyy-MM-dd`. Se muestra con `formatearFecha`, nunca con `new Date(iso)`. */
  fecha: string
  numeroRemito: string | null
  origen: string
  destino: string
  importe: number
  puedeFacturarse: boolean
  motivoNoFacturable: 'sinRemito' | null
}

/**
 * Lo que manda el alta, y lo mismo que recibe la vista previa.
 *
 * **No lleva `neto`, `iva` ni `total`**, y eso es el requisito y no una omisión: los calcula el
 * servidor a partir de los viajes que encuentra en la base, así que no hay forma de mandarlos ni desde
 * la pantalla ni invocando la acción a mano (FR-024).
 */
export interface EmisionPeticion {
  clienteId: number
  tipoComprobante: TipoComprobante
  tipoFacturacion: TipoFacturacion
  condicionDeVenta: CondicionDeVenta
  mes: number
  anio: number
  fecha: string
  numeroComprobante: string
  detalle: string | null
  cae: string
  caeVencimiento: string
  vencimientoPago: string
  facturaReemplazadaId: number | null
  viajeIds: number[]
  /** Sólo hace falta después de un `409 emision_requiere_confirmacion` (FR-032). */
  confirmado?: boolean
}

/** Los viajes del cliente, en `rendido`, con fecha en el período y sin factura vigente (FR-015). */
export function listarFacturables(clienteId: number, mes: number, anio: number) {
  return obtener<ViajeFacturable[]>(`/facturas/facturables${query({ clienteId, mes, anio })}`)
}

/**
 * Si la empresa emisora está configurada, para avisarlo **antes** de completar todo el formulario.
 *
 * Es una cortesía y no la validación: el rechazo de FR-006 llega igual del servidor al emitir. Sin este
 * aviso, quien opera carga trece campos y elige viajes para enterarse al final de que faltaba
 * configurar algo de otra pantalla.
 */
export function obtenerEmpresaEmisoraParaAlta() {
  return obtener<{ configurada: boolean; faltantes: string[] }>('/facturacion/empresa-emisora')
}

/** Las anuladas de ese cliente que todavía nadie refacturó (FR-049, FR-049a). */
export function listarAnuladasSinReemplazo(clienteId: number) {
  return obtener<FacturaResumen[]>(`/facturas/anuladas-sin-reemplazo${query({ clienteId })}`)
}

/**
 * El documento tal como va a quedar, **generado por el servidor** (FR-033).
 *
 * Devuelve un `Blob` y no JSON: es el mismo PDF que se va a guardar al emitir. **No es una maqueta
 * dibujada en React**, y eso es lo que hace que revisar la vista previa sirva para algo — dos maquetas
 * paralelas se separan sin que nadie lo note (research §2).
 *
 * Pedirla no crea la factura ni guarda ningún archivo (US2 esc. 33).
 */
export function pedirVistaPrevia(peticion: EmisionPeticion) {
  return obtenerPdf('/facturas/vista-previa', { metodo: 'POST', cuerpo: peticion })
}

/**
 * Emite la factura.
 *
 * **Las dos confirmaciones de FR-032 llegan como `409` sin haber creado nada**: la pantalla abre el
 * diálogo que corresponda según `motivoConfirmacion` y reintenta con `confirmado: true`. Viven en el
 * backend porque la emisión no se deshace (convención [005]).
 */
export function emitirFactura(peticion: EmisionPeticion) {
  return enviar<FacturaDetalle>('/facturas', peticion)
}

// ── Facturas ────────────────────────────────────────────────────────────────────────────────────

/** Lo que el listado necesita del cliente: la copia congelada y si sigue activo. */
export interface ClienteResumido {
  id: number
  /** La **congelada en la factura**, no la del padrón (FR-034a). */
  razonSocial: string
  /** Del padrón. `false` se muestra con la palabra `Inactivo` al lado, nunca sólo con color (FR-011). */
  activo: boolean
}

/** Fila del listado: las ocho columnas de FR-057. */
export interface FacturaListado {
  id: number
  numeroComprobante: string
  fecha: string
  cliente: ClienteResumido
  tipoComprobante: TipoComprobante
  mes: number
  anio: number
  total: number
  estado: EstadoFacturaVisible
  vencimientoPago: string
  motivoAnulacion: string | null
  fechaCobro: string | null
}

/** Los diez datos del emisor congelados al emitir (FR-034). El logo no se congela. */
export interface EmisorDeFactura {
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

export interface ClienteDeFactura {
  id: number
  razonSocial: string
  cuit: string
  domicilio: string
  activo: boolean
}

export interface ViajeDeFactura {
  id: number
  numero: number
  fecha: string
  numeroRemito: string | null
  origen: string
  destino: string
  importe: number
}

/**
 * Una línea del historial (FR-045, FR-037).
 *
 * `estadoNuevo` en `null` marca una **corrección de datos**, que no cambió ningún estado: el sistema
 * registra quién y cuándo, y no qué campos cambiaron.
 */
export interface EntradaDeHistorial {
  estadoAnterior: string | null
  estadoNuevo: string | null
  usuario: string
  /** Instante UTC con la `Z`. Se muestra en hora local con `formatearInstante` (convención [002]). */
  ocurridoEn: string
}

/** Ficha completa (FR-060). */
export interface FacturaDetalle {
  id: number
  numeroComprobante: string
  fecha: string
  tipoComprobante: TipoComprobante
  tipoFacturacion: TipoFacturacion
  condicionDeVenta: CondicionDeVenta
  mes: number
  anio: number
  detalle: string | null
  emisor: EmisorDeFactura
  cliente: ClienteDeFactura
  viajes: ViajeDeFactura[]
  neto: number
  iva: number
  /** Derivada del tipo de comprobante, no almacenada. Llega calculada del servidor (research §5). */
  alicuota: number
  total: number
  cae: string
  caeVencimiento: string
  vencimientoPago: string
  estado: EstadoFacturaVisible
  fechaCobro: string | null
  motivoAnulacion: string | null
  /** A qué factura anulada reemplaza esta Refacturación (FR-050). */
  reemplazaA: FacturaResumen | null
  /** Qué Refacturación reemplazó a esta anulada (FR-050). */
  reemplazadaPor: FacturaResumen | null
  documentoUrl: string
  historial: EntradaDeHistorial[]
}

/**
 * Los cinco filtros del listado (FR-058).
 *
 * `estado` vacío significa **todas, incluidas las anuladas** —al revés que el listado de viajes— y el
 * control lo dice con todas las letras (FR-064).
 */
export interface FiltrosFacturas {
  clienteId: number | ''
  desde: string
  hasta: string
  mes: number | ''
  anio: number | ''
  estado: EstadoFacturaVisible | ''
  tipoComprobante: TipoComprobante | ''
}

export const FILTROS_FACTURAS_INICIALES: FiltrosFacturas = {
  clienteId: '',
  desde: '',
  hasta: '',
  mes: '',
  anio: '',
  estado: '',
  tipoComprobante: '',
}

export function listarFacturas(filtros: FiltrosFacturas, pagina: number) {
  return obtener<PaginaDe<FacturaListado>>(
    `/facturas${query({
      clienteId: filtros.clienteId,
      desde: filtros.desde,
      hasta: filtros.hasta,
      mes: filtros.mes,
      anio: filtros.anio,
      estado: filtros.estado,
      tipoComprobante: filtros.tipoComprobante,
      pagina,
    })}`,
  )
}

export function obtenerFactura(id: number) {
  return obtener<FacturaDetalle>(`/facturas/${id}`)
}

/** Sólo cuatro campos editables: el resto de una factura emitida no se modifica (FR-035, FR-036). */
export interface CorreccionPeticion {
  detalle: string | null
  cae: string
  caeVencimiento: string
  vencimientoPago: string
}

export function corregirFactura(id: number, peticion: CorreccionPeticion) {
  return actualizar<FacturaDetalle>(`/facturas/${id}`, peticion)
}

// ── Ciclo de vida: cada cambio de estado es un recurso propio (FR-044) ──────────────────────────

/** `pagada` es terminal: no existe ninguna acción que revierta un cobro (FR-043). */
export function registrarCobro(id: number, fechaCobro: string) {
  return enviar<FacturaDetalle>(`/facturas/${id}/cobro`, { fechaCobro })
}

/** El motivo es obligatorio y la confirmación la pide la pantalla (FR-046). */
export function anularFactura(id: number, motivo: string) {
  return enviar<FacturaDetalle>(`/facturas/${id}/anulacion`, { motivo })
}

// ── Reportes ────────────────────────────────────────────────────────────────────────────────────

/** Una fila del panel de vencimientos (FR-063). */
export interface FilaDeVencimiento {
  id: number
  numeroComprobante: string
  cliente: string
  total: number
  vencimientoPago: string
  /** Negativo = días de atraso. Positivo o cero = días de plazo. */
  dias: number
}

export function consultarVencimientos() {
  return obtener<FilaDeVencimiento[]>('/facturas/vencimientos')
}

/** Una fila del cuadro de totales (FR-061). */
export interface TotalPorCliente {
  clienteId: number
  razonSocial: string
  /** Facturas **no anuladas** del rango (FR-062). */
  cantidad: number
  facturado: number
  cobrado: number
  pendiente: number
}

/** El rango es **obligatorio**: sin él el backend responde `rango_de_fechas_requerido` (FR-061). */
export function consultarTotalesFacturados(desde: string, hasta: string) {
  return obtener<TotalPorCliente[]>(`/facturas/totales${query({ desde, hasta })}`)
}

/** El nombre con `Inactivo` cuando corresponde. Nunca sólo un color (FR-011, FR-065). */
export function nombreDeCliente(cliente: { razonSocial: string; activo: boolean }): string {
  return cliente.activo ? cliente.razonSocial : `${cliente.razonSocial} (Inactivo)`
}
