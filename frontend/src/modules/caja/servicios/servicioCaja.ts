import { ErrorHttp, peticion } from '../../../compartido/clienteHttp'
import type { ErrorApi } from '../../../compartido/tipos'

/**
 * Acceso HTTP del Módulo 11 y los tipos de `contracts/README.md`.
 *
 * **Las rutas de acá no llevan `/api`**: se lo antepone `peticion` (precedente del Módulo 3).
 *
 * **Copias contadas al empezar la feature** (convención [010], tasks T003): `esSinPermiso` y el aviso de
 * pantalla sin permiso existían sólo en adelantos —esta es la segunda copia—. `detalleDeError` está en
 * facturación, liquidaciones y adelantos, y esta es la cuarta. No es un formato de presentación sino la
 * conversión del cuerpo al tipo de error de cada módulo, así que [009] no la alcanza, y el plan no toca
 * archivos de otros módulos: se copia. `MENSAJE_ACCION_SIN_PERMISO` es texto propio del módulo.
 */

export type EstadoCaja = 'abierta' | 'cerrada'

export type TipoMovimientoCaja = 'ingreso' | 'egreso'

export interface UsuarioResumen {
  id: number
  /** El nombre de usuario vigente. */
  nombre: string
}

export interface CajaListado {
  id: number
  responsable: UsuarioResumen
  /** Instante UTC con la `Z`. Se muestra con `formatearInstante` (convención [002]). */
  fechaApertura: string
  estado: EstadoCaja
  /** Sólo en las cerradas. */
  fechaCierre: string | null
  saldoFinal: number | null
}

export interface CajaDetalle extends CajaListado {
  saldoInicial: number
  totalIngresos: number
  totalEgresos: number
  /** Inicial + ingresos − egresos, calculado por el servidor al leer. */
  saldoActual: number
  /** Abierta y del usuario en sesión: lo decide el servidor (FR-035). */
  puedeOperar: boolean
}

export interface MovimientoListado {
  id: number
  cajaId: number
  fecha: string
  tipo: TipoMovimientoCaja
  importe: number
  concepto: string
  responsable: UsuarioResumen
  /** Armada por el backend —`Factura 0001-00000012 · Cliente SA` u `OP-7`— o `null`. */
  referencia: string | null
}

export interface ResumenDeCierre {
  saldoInicial: number
  totalIngresos: number
  totalEgresos: number
  /** En orden cronológico. */
  movimientos: MovimientoListado[]
  saldoFinal: number
}

export interface OpcionDeReferencia {
  id: number
  texto: string
}

export interface Pagina<T> {
  items: T[]
  total: number
  pagina: number
  tamanioPagina: number
}

export type PaginaDeCajas = Pagina<CajaListado>

export type PaginaDeMovimientos = Pagina<MovimientoListado>

/** Los cuerpos de error del contrato en uno: los del cierre traen además el resumen. */
export interface ErrorDeCaja extends ErrorApi, Partial<ResumenDeCierre> {}

export const CodigosErrorCaja = {
  datosInvalidos: 'datos_invalidos',
  referenciaInvalida: 'referencia_invalida',
  rangoInvalido: 'rango_invalido',
  cajaNoEncontrada: 'caja_no_encontrada',
  cajaYaAbierta: 'caja_ya_abierta',
  cajaCerrada: 'caja_cerrada',
  cajaAjena: 'caja_ajena',
  confirmacionRequerida: 'confirmacion_requerida',
  cierreDesactualizado: 'cierre_desactualizado',
} as const

// ── Cómo se nombra cada cosa en pantalla (`contracts/README.md`) ─────────────────────────────────

export const NOMBRES_DE_ESTADO: Record<EstadoCaja, string> = {
  abierta: 'Abierta',
  cerrada: 'Cerrada',
}

export const NOMBRES_DE_TIPO: Record<TipoMovimientoCaja, string> = {
  ingreso: 'Ingreso',
  egreso: 'Egreso',
}

/** Sin caja operable al registrar (CA3): no existe, está cerrada o es de otro. */
export const MENSAJE_SIN_CAJA_ABIERTA =
  'No hay una caja abierta. Abrí una caja para poder registrar movimientos.'

export const MENSAJE_INESPERADO = 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.'

/** El texto de una acción rechazada por falta de permiso (FR-033). */
export const MENSAJE_ACCION_SIN_PERMISO = 'No tenés permiso para abrir, operar ni cerrar cajas.'

/** El aviso de una pantalla a la que se llega sin permiso escribiendo la dirección (convención [010]). */
export const MENSAJE_SIN_PERMISO =
  'No tenés permiso para ver la caja. Si lo necesitás, pedíselo a quien administra los usuarios del sistema.'

/** El cuerpo del error con los campos del módulo, o `null` si no es un error del backend. */
export function detalleDeError(error: unknown): ErrorDeCaja | null {
  return error instanceof ErrorHttp ? (error.detalle as ErrorDeCaja) : null
}

/** Un `403`: falta el permiso. "Volvé a intentar" es falso para quien no lo tiene (convención [010]). */
export function esSinPermiso(fallo: unknown): boolean {
  return fallo instanceof ErrorHttp && fallo.estado === 403
}

/** Lo que se muestra de una acción rechazada: el texto de permiso, el del servidor, o el genérico. */
export function mensajeDeRechazo(fallo: unknown): string {
  return esSinPermiso(fallo) ? MENSAJE_ACCION_SIN_PERMISO : (detalleDeError(fallo)?.mensaje ?? MENSAJE_INESPERADO)
}

/** Arma la query descartando los parámetros vacíos. */
function query(parametros: Record<string, string | number>) {
  const partes = new URLSearchParams()

  for (const [nombre, valor] of Object.entries(parametros)) {
    if (valor !== '') {
      partes.append(nombre, String(valor))
    }
  }

  const texto = partes.toString()

  return texto === '' ? '' : `?${texto}`
}

// ── Cajas ───────────────────────────────────────────────────────────────────────────────────────

/** La caja abierta propia, o `null` si no tiene ninguna (`204`). */
export async function obtenerMiCajaAbierta(): Promise<CajaDetalle | null> {
  return (await peticion<CajaDetalle | undefined>('/caja/abierta')) ?? null
}

export function listarCajas(pagina: number) {
  return peticion<PaginaDeCajas>(`/caja${query({ pagina })}`)
}

export function obtenerCaja(id: number) {
  return peticion<CajaDetalle>(`/caja/${id}`)
}

export function abrirCaja(saldoInicial: number) {
  return peticion<CajaDetalle>('/caja', { metodo: 'POST', cuerpo: { saldoInicial } })
}

// ── Movimientos ─────────────────────────────────────────────────────────────────────────────────

export function registrarMovimiento(
  cajaId: number,
  peticionDeMovimiento: {
    tipo: TipoMovimientoCaja
    importe: number
    concepto: string
    facturaId?: number
    ordenDePagoId?: number
  },
) {
  return peticion<MovimientoListado>(`/caja/${cajaId}/movimientos`, {
    metodo: 'POST',
    cuerpo: peticionDeMovimiento,
  })
}

export function listarMovimientosDeCaja(cajaId: number, pagina: number) {
  return peticion<PaginaDeMovimientos>(`/caja/${cajaId}/movimientos${query({ pagina })}`)
}

/** El desplegable del ingreso: sólo facturas pendientes de cobro. */
export function listarFacturasPendientes() {
  return peticion<OpcionDeReferencia[]>('/caja/facturas-pendientes')
}

/** El desplegable del egreso: las 50 órdenes de pago más recientes, y la pantalla lo dice (FR-011). */
export function listarOrdenesDePago() {
  return peticion<OpcionDeReferencia[]>('/caja/ordenes-de-pago')
}

// ── Cierre ──────────────────────────────────────────────────────────────────────────────────────

export function obtenerResumenDeCierre(cajaId: number) {
  return peticion<ResumenDeCierre>(`/caja/${cajaId}/cierre`)
}

export type ResultadoDeCierre =
  | { tipo: 'cerrada'; caja: CajaDetalle }
  | { tipo: 'desactualizado'; codigo: string; mensaje: string; resumen: ResumenDeCierre }

/**
 * Siempre con `confirmado: true`: **la pantalla de resumen es la confirmación**, y manda el saldo que está
 * mostrando. Los dos `409` que traen el resumen vuelven como **resultado** y no como excepción, para que la
 * pantalla lo reemplace y pida confirmar de nuevo (convención [009]). El resto se lanza.
 */
export async function cerrarCaja(cajaId: number, saldoFinalConfirmado: number): Promise<ResultadoDeCierre> {
  try {
    const caja = await peticion<CajaDetalle>(`/caja/${cajaId}/cierre`, {
      metodo: 'POST',
      cuerpo: { confirmado: true, saldoFinalConfirmado },
    })

    return { tipo: 'cerrada', caja }
  } catch (fallo) {
    const detalle = detalleDeError(fallo)

    if (
      fallo instanceof ErrorHttp &&
      fallo.estado === 409 &&
      (detalle?.codigo === CodigosErrorCaja.cierreDesactualizado ||
        detalle?.codigo === CodigosErrorCaja.confirmacionRequerida)
    ) {
      return {
        tipo: 'desactualizado',
        codigo: detalle.codigo,
        mensaje: detalle.mensaje,
        resumen: {
          saldoInicial: detalle.saldoInicial ?? 0,
          totalIngresos: detalle.totalIngresos ?? 0,
          totalEgresos: detalle.totalEgresos ?? 0,
          movimientos: detalle.movimientos ?? [],
          saldoFinal: detalle.saldoFinal ?? 0,
        },
      }
    }

    throw fallo
  }
}

// ── Consulta global ─────────────────────────────────────────────────────────────────────────────

export interface FiltrosMovimientos {
  /** `yyyy-MM-dd` o vacío: sin límite. Días de Argentina (tasks.md decisión 4). */
  desde: string
  hasta: string
  cajaId: number | ''
}

export const FILTROS_MOVIMIENTOS_INICIALES: FiltrosMovimientos = {
  desde: '',
  hasta: '',
  cajaId: '',
}

/** FR-023. Dos `yyyy-MM-dd` se comparan como texto. */
export function rangoInvertido(filtros: FiltrosMovimientos): boolean {
  return filtros.desde !== '' && filtros.hasta !== '' && filtros.desde > filtros.hasta
}

export function listarMovimientos(filtros: FiltrosMovimientos, pagina: number) {
  return peticion<PaginaDeMovimientos>(
    `/movimientos-caja${query({
      desde: filtros.desde,
      hasta: filtros.hasta,
      cajaId: filtros.cajaId,
      pagina,
    })}`,
  )
}
