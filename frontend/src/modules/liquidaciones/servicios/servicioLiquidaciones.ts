import { ErrorHttp, peticion } from '../../../compartido/clienteHttp'
import type { ErrorApi } from '../../../compartido/tipos'

/**
 * Acceso HTTP del Módulo 9 y los tipos de `contracts/liquidaciones-api.yaml`.
 *
 * **Las rutas de acá no llevan `/api`**: se lo antepone `peticion` (precedente del Módulo 3).
 *
 * **Los números de liquidación y de orden de pago llegan armados** —`LQ-12`, `OP-3`— y se muestran tal
 * cual: escribir el formato también en TypeScript serían dos formatos que se pueden separar (research §4).
 */

export type EstadoLiquidacion = 'pendiente' | 'pagada' | 'anulada'

export type OperacionDeLiquidacion = 'generacion' | 'edicion' | 'anulacion' | 'pagada'

export type MotivoDeBloqueo = 'pagada' | 'anulada' | 'conOrdenesDePago' | 'transportistaNoLiquidable'

export interface TransportistaResumen {
  id: number
  razonSocial: string
  /** Once dígitos: los guiones los pone `compartido/cuit`. */
  cuit: string
  /** Del padrón. `false` se muestra con la palabra `Inactivo`. */
  activo: boolean
}

export interface TransportistasLiquidables {
  /** `false` con la lista vacía: la generación muestra el bloqueo con su salida (FR-001a). */
  empresaEmisoraConfigurada: boolean
  transportistas: TransportistaResumen[]
}

export interface ViajeDisponible {
  id: number
  numero: number
  /** `yyyy-MM-dd`. Se muestra con `formatearFecha`, nunca con `new Date(iso)`. */
  fecha: string
  origen: string
  destino: string
  estado: 'rendido' | 'facturado'
  importe: number
}

export interface ViajeLiquidado {
  id: number
  numero: number
  fecha: string
  origen: string
  destino: string
  importe: number
}

export interface LiquidacionListado {
  id: number
  numero: string
  mes: number
  anio: number
  transportista: TransportistaResumen
  importeTotal: number
  /** `null` en una anulada: no se debe nada (FR-027). */
  restaPagar: number | null
  estado: EstadoLiquidacion
  motivoAnulacion: string | null
}

export interface OrdenDePago {
  id: number
  numero: string
  fechaPago: string
  importe: number
  registradaPor: string
  /** Instante UTC con la `Z`. Se muestra con `formatearInstante` (convención [002]). */
  registradaEn: string
}

export interface CambioDeLiquidacion {
  operacion: OperacionDeLiquidacion
  usuario: string
  ocurridoEn: string
  /** Números de viaje. Vacíos salvo en una edición (FR-033). */
  viajesQuitados: number[]
  viajesAgregados: number[]
}

export interface LiquidacionDetalle {
  id: number
  numero: string
  mes: number
  anio: number
  fechaGeneracion: string
  transportista: TransportistaResumen
  estado: EstadoLiquidacion
  motivoAnulacion: string | null
  importeTotal: number
  importePagado: number
  restaPagar: number | null
  /** La edición la manda de vuelta al guardar (FR-048). */
  version: number
  /** Los vigentes, o los que agrupaba si está anulada (FR-028). */
  viajes: ViajeLiquidado[]
  ordenesDePago: OrdenDePago[]
  historial: CambioDeLiquidacion[]
  /** Por estado, pagos y transportista. Que el usuario tenga el permiso lo decide la sesión. */
  puedeEditarse: boolean
  puedeAnularse: boolean
  puedeRegistrarPago: boolean
}

export interface PaginaDe<T> {
  items: T[]
  total: number
  pagina: number
  tamanioPagina: number
}

export interface ViajeEnConflicto {
  id: number
  numero: number
  motivo: 'yaLiquidado' | 'noRendidoNiFacturado' | 'deOtroTransportista' | 'deOtroPeriodo'
  /** La liquidación que lo tiene, armada, con `yaLiquidado`. */
  liquidacion: string | null
}

/** Los cuerpos de error del contrato en uno: los campos que un rechazo no lleva no vienen. */
export interface ErrorDeLiquidacion extends ErrorApi {
  viajes?: ViajeEnConflicto[]
  motivo?: MotivoDeBloqueo
  cantidadOrdenesDePago?: number
  importePagado?: number
  restaPagar?: number
  desde?: string
  hasta?: string
}

/** El `409` de FR-040: los importes que calculó el servidor sobre el saldo actual. */
export interface ConfirmacionDePago {
  importe: number
  restaPagarAntes: number
  restaPagarDespues: number
  quedaPagada: boolean
}

export const CodigosErrorLiquidaciones = {
  datosInvalidos: 'datos_invalidos',
  periodoInvalido: 'periodo_invalido',
  empresaEmisoraNoConfigurada: 'empresa_emisora_no_configurada',
  transportistaNoLiquidable: 'transportista_no_liquidable',
  sinViajes: 'sin_viajes',
  totalEnCero: 'total_en_cero',
  viajeNoLiquidable: 'viaje_no_liquidable',
  fechaDePagoFueraDeRango: 'fecha_de_pago_fuera_de_rango',
  importeSuperaSaldo: 'importe_supera_saldo',
  motivoRequerido: 'motivo_requerido',
  liquidacionNoEncontrada: 'liquidacion_no_encontrada',
  viajeYaLiquidado: 'viaje_ya_liquidado',
  liquidacionNoEditable: 'liquidacion_no_editable',
  liquidacionModificada: 'liquidacion_modificada',
  liquidacionNoAnulable: 'liquidacion_no_anulable',
  liquidacionNoPagable: 'liquidacion_no_pagable',
  pagoRequiereConfirmacion: 'pago_requiere_confirmacion',
  anulacionRequiereConfirmacion: 'anulacion_requiere_confirmacion',
} as const

// ── Cómo se nombra cada cosa en pantalla (`contracts/README.md`) ─────────────────────────────────

export const NOMBRES_DE_ESTADO: Record<EstadoLiquidacion, string> = {
  pendiente: 'Pendiente',
  pagada: 'Pagada',
  anulada: 'Anulada',
}

/** Con la minúscula que pide la oración: `Mostrando sólo las liquidaciones pendientes.` */
export const ESTADO_EN_ORACION: Record<EstadoLiquidacion, string> = {
  pendiente: 'pendientes',
  pagada: 'pagadas',
  anulada: 'anuladas',
}

/** Las mismas opciones de período que el Módulo 6 (FR-002). Los años salen de `compartido/fechas`. */
export const MESES = Array.from({ length: 12 }, (_, indice) => indice + 1)

/** `07/2026`. */
export function formatearPeriodo(mes: number, anio: number): string {
  return `${String(mes).padStart(2, '0')}/${anio}`
}

/** `1 viaje` · `3 viajes`. */
export function cantidadDeViajes(cantidad: number): string {
  return `${cantidad} ${cantidad === 1 ? 'viaje' : 'viajes'}`
}

/** El cuerpo del error con los campos del módulo, o `null` si no es un error del backend. */
export function detalleDeError(error: unknown): ErrorDeLiquidacion | null {
  return error instanceof ErrorHttp ? (error.detalle as ErrorDeLiquidacion) : null
}

/** Arma la query descartando los parámetros vacíos. */
function query(parametros: Record<string, string | number | boolean>) {
  const partes = new URLSearchParams()

  for (const [nombre, valor] of Object.entries(parametros)) {
    if (valor !== '') {
      partes.append(nombre, String(valor))
    }
  }

  const texto = partes.toString()

  return texto === '' ? '' : `?${texto}`
}

// ── Armado ──────────────────────────────────────────────────────────────────────────────────────

/** Sin `incluirInactivos`, los que se ofrecen para generar; con él, los del filtro del listado. */
export function listarTransportistas(incluirInactivos = false) {
  return peticion<TransportistasLiquidables>(
    `/liquidaciones/transportistas${incluirInactivos ? query({ incluirInactivos }) : ''}`,
  )
}

/** Los viajes disponibles (FR-004). La usan la generación y también la edición. */
export function listarDisponibles(transportistaId: number, mes: number, anio: number) {
  return peticion<ViajeDisponible[]>(
    `/liquidaciones/disponibles${query({ transportistaId, mes, anio })}`,
  )
}

/** **Sin importes**: el total lo calcula el servidor con los viajes de la base (FR-007). */
export function generarLiquidacion(peticionDeGeneracion: {
  transportistaId: number
  mes: number
  anio: number
  viajeIds: number[]
}) {
  return peticion<LiquidacionDetalle>('/liquidaciones', {
    metodo: 'POST',
    cuerpo: peticionDeGeneracion,
  })
}

// ── Consulta ────────────────────────────────────────────────────────────────────────────────────

export function obtenerLiquidacion(id: number) {
  return peticion<LiquidacionDetalle>(`/liquidaciones/${id}`)
}

/** `estado` vacío significa **todas, incluidas las anuladas**, y el control lo dice. */
export interface FiltrosLiquidaciones {
  transportistaId: number | ''
  mes: number | ''
  anio: number | ''
  estado: EstadoLiquidacion | ''
}

export const FILTROS_LIQUIDACIONES_INICIALES: FiltrosLiquidaciones = {
  transportistaId: '',
  mes: '',
  anio: '',
  estado: '',
}

export function listarLiquidaciones(filtros: FiltrosLiquidaciones, pagina: number) {
  return peticion<PaginaDe<LiquidacionListado>>(
    `/liquidaciones${query({
      transportistaId: filtros.transportistaId,
      mes: filtros.mes,
      anio: filtros.anio,
      estado: filtros.estado,
      pagina,
    })}`,
  )
}

// ── Escrituras ──────────────────────────────────────────────────────────────────────────────────

export type ResultadoDeOrdenDePago =
  | { tipo: 'registrada'; liquidacion: LiquidacionDetalle }
  | { tipo: 'requiereConfirmacion'; confirmacion: ConfirmacionDePago }

/**
 * Registra una orden de pago.
 *
 * **El `409` de confirmación vuelve como resultado y no como excepción**: no es un fracaso, es el primer
 * paso del diálogo. Los importes que trae son los que el diálogo muestra (research §7).
 */
export async function registrarOrdenDePago(
  id: number,
  orden: { fechaPago: string; importe: number; confirmado?: boolean },
): Promise<ResultadoDeOrdenDePago> {
  try {
    const liquidacion = await peticion<LiquidacionDetalle>(`/liquidaciones/${id}/ordenes-de-pago`, {
      metodo: 'POST',
      cuerpo: orden,
    })

    return { tipo: 'registrada', liquidacion }
  } catch (fallo) {
    if (
      fallo instanceof ErrorHttp &&
      fallo.detalle.codigo === CodigosErrorLiquidaciones.pagoRequiereConfirmacion
    ) {
      const { importe, restaPagarAntes, restaPagarDespues, quedaPagada } =
        fallo.detalle as unknown as ConfirmacionDePago

      return {
        tipo: 'requiereConfirmacion',
        confirmacion: { importe, restaPagarAntes, restaPagarDespues, quedaPagada },
      }
    }

    throw fallo
  }
}

/** El conjunto final de viajes y la versión con que se abrió la edición (FR-048). */
export function editarLiquidacion(id: number, viajeIds: number[], version: number) {
  return peticion<LiquidacionDetalle>(`/liquidaciones/${id}`, {
    metodo: 'PUT',
    cuerpo: { viajeIds, version },
  })
}

/**
 * Siempre con `confirmado: true`: **el diálogo es la confirmación explícita**. El `409` sigue ahí para
 * quien invoca la acción sin pasar por la pantalla (research §7, FR-056).
 */
export function anularLiquidacion(id: number, motivo: string) {
  return peticion<LiquidacionDetalle>(`/liquidaciones/${id}/anulacion`, {
    metodo: 'POST',
    cuerpo: { motivo, confirmado: true },
  })
}
