import { startOfMonth, subMonths } from 'date-fns'
import { ErrorHttp, peticion } from '../../../compartido/clienteHttp'
import { enIso } from '../../../compartido/fechas'
import type { ErrorApi } from '../../../compartido/tipos'

/**
 * Acceso HTTP del Módulo 10 y los tipos de `contracts/adelantos-api.yaml`.
 *
 * **Las rutas de acá no llevan `/api`**: se lo antepone `peticion` (precedente del Módulo 3).
 */

export type EstadoAdelanto = 'pendiente' | 'aprobado' | 'rechazado' | 'anulado'

export type TipoBeneficiario = 'chofer' | 'empleado'

export type OperacionDeAdelanto = 'registro' | 'aprobacion' | 'rechazo' | 'anulacion'

export type MotivoNoElegible = 'inexistente' | 'inactiva' | 'tipoDistinto' | 'choferExterno'

/** Siempre del padrón vigente (FR-020). */
export interface PersonaResumen {
  id: number
  apellido: string
  nombre: string
  /** Sólo dígitos. */
  dni: string
}

export interface Beneficiarios {
  /** Sólo decide algo con tipo chofer (FR-002a). */
  empresaEmisoraConfigurada: boolean
  personas: PersonaResumen[]
}

export interface AdelantoListado {
  id: number
  /** `yyyy-MM-dd`. Se muestra con `formatearFecha`, nunca con `new Date(iso)`. */
  fecha: string
  persona: PersonaResumen
  tipo: TipoBeneficiario
  motivo: string
  importe: number
  estado: EstadoAdelanto
}

/** La forma de paginación de [003] con un campo más. */
export interface PaginaDeAdelantos {
  items: AdelantoListado[]
  total: number
  pagina: number
  tamanioPagina: number
  /** Suma de los aprobados entre **todos** los que cumplen los filtros (FR-016). */
  totalAdelantado: number
}

export interface CambioDeAdelanto {
  operacion: OperacionDeAdelanto
  usuario: string
  /** Instante UTC con la `Z`. Se muestra con `formatearInstante` (convención [002]). */
  ocurridoEn: string
  /** En rechazo y anulación; `null` en las otras. */
  motivo: string | null
}

export interface AdelantoDetalle {
  id: number
  fecha: string
  persona: PersonaResumen
  /** El elegido al registrar; no se recalcula (FR-020). */
  tipo: TipoBeneficiario
  motivo: string
  importe: number
  estado: EstadoAdelanto
  motivoRechazo: string | null
  motivoAnulacion: string | null
  historial: CambioDeAdelanto[]
  /** Sólo por estado. Que el usuario tenga el permiso lo decide la sesión. */
  puedeResolverse: boolean
  puedeAnularse: boolean
}

/** Los cuerpos de error del contrato en uno: los campos que un rechazo no lleva no vienen. */
export interface ErrorDeAdelanto extends ErrorApi {
  desde?: string
  hasta?: string
  motivo?: MotivoNoElegible
  estado?: EstadoAdelanto
}

export const CodigosErrorAdelantos = {
  datosInvalidos: 'datos_invalidos',
  fechaFueraDeRango: 'fecha_fuera_de_rango',
  empresaEmisoraNoConfigurada: 'empresa_emisora_no_configurada',
  beneficiarioNoElegible: 'beneficiario_no_elegible',
  motivoRequerido: 'motivo_requerido',
  rangoInvalido: 'rango_invalido',
  adelantoNoEncontrado: 'adelanto_no_encontrado',
  adelantoNoResoluble: 'adelanto_no_resoluble',
  adelantoNoAnulable: 'adelanto_no_anulable',
  rechazoRequiereConfirmacion: 'rechazo_requiere_confirmacion',
  anulacionRequiereConfirmacion: 'anulacion_requiere_confirmacion',
} as const

// ── Cómo se nombra cada cosa en pantalla (`contracts/README.md`) ─────────────────────────────────

export const NOMBRES_DE_ESTADO: Record<EstadoAdelanto, string> = {
  pendiente: 'Pendiente',
  aprobado: 'Aprobado',
  rechazado: 'Rechazado',
  anulado: 'Anulado',
}

/** Con la minúscula que pide la oración: `Mostrando sólo los adelantos pendientes.` */
export const ESTADO_EN_ORACION: Record<EstadoAdelanto, string> = {
  pendiente: 'pendientes',
  aprobado: 'aprobados',
  rechazado: 'rechazados',
  anulado: 'anulados',
}

export const NOMBRES_DE_TIPO: Record<TipoBeneficiario, string> = {
  chofer: 'Chofer',
  empleado: 'Empleado',
}

/** `Pérez, Juan`. */
export function formatearPersona(persona: Pick<PersonaResumen, 'apellido' | 'nombre'>): string {
  return `${persona.apellido}, ${persona.nombre}`
}

const MENSAJE_INESPERADO = 'Ocurrió un problema inesperado. Volvé a intentar en unos minutos.'

/** El texto de una acción rechazada por falta de permiso (FR-043, contracts/README §Pantallas). */
export const MENSAJE_ACCION_SIN_PERMISO = 'No tenés permiso para registrar, aprobar, rechazar ni anular adelantos.'

/** El cuerpo del error con los campos del módulo, o `null` si no es un error del backend. */
export function detalleDeError(error: unknown): ErrorDeAdelanto | null {
  return error instanceof ErrorHttp ? (error.detalle as ErrorDeAdelanto) : null
}

/**
 * Un `403`: falta el permiso. Se distingue de un error de carga porque "volvé a intentar" es falso para
 * quien no tiene permiso (FR-043, research §7).
 */
export function esSinPermiso(fallo: unknown): boolean {
  return fallo instanceof ErrorHttp && fallo.estado === 403
}

/** Lo que se muestra de una acción rechazada: el texto de permiso, el del servidor, o el genérico. */
export function mensajeDeRechazo(fallo: unknown): string {
  return esSinPermiso(fallo) ? MENSAJE_ACCION_SIN_PERMISO : (detalleDeError(fallo)?.mensaje ?? MENSAJE_INESPERADO)
}

/**
 * El primer día del mes anterior a `hoy`, en `yyyy-MM-dd` (FR-005).
 *
 * **Derivado de hoy y nunca escrito literal** (convención [009]). Con `date-fns` sobre el primero del mes
 * y no restando uno al mes a mano, que en enero daría el mes cero. El servidor aplica la misma regla y es
 * quien la garantiza.
 */
export function primeraFechaAdmitida(hoy: Date = new Date()): string {
  return enIso(startOfMonth(subMonths(hoy, 1)))
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

// ── Registro ────────────────────────────────────────────────────────────────────────────────────

/** Las personas elegibles de un tipo, con la misma regla que valida el registro (research §1). */
export function listarBeneficiarios(tipo: TipoBeneficiario) {
  return peticion<Beneficiarios>(`/adelantos/beneficiarios${query({ tipo })}`)
}

export function registrarAdelanto(peticionDeRegistro: {
  tipo: TipoBeneficiario
  personaId: number
  fecha: string
  motivo: string
  importe: number
}) {
  return peticion<AdelantoDetalle>('/adelantos', { metodo: 'POST', cuerpo: peticionDeRegistro })
}

// ── Consulta ────────────────────────────────────────────────────────────────────────────────────

export function obtenerAdelanto(id: number) {
  return peticion<AdelantoDetalle>(`/adelantos/${id}`)
}

/** `estado` vacío significa **todos, incluidos rechazados y anulados**, y el control lo dice. */
export interface FiltrosAdelantos {
  personaId: number | ''
  /** `yyyy-MM-dd` o vacío: sin límite. */
  desde: string
  hasta: string
  estado: EstadoAdelanto | ''
}

export const FILTROS_ADELANTOS_INICIALES: FiltrosAdelantos = {
  personaId: '',
  desde: '',
  hasta: '',
  estado: '',
}

/** FR-015. Dos `yyyy-MM-dd` se comparan como texto. */
export function rangoInvertido(filtros: FiltrosAdelantos): boolean {
  return filtros.desde !== '' && filtros.hasta !== '' && filtros.desde > filtros.hasta
}

export function listarAdelantos(filtros: FiltrosAdelantos, pagina: number) {
  return peticion<PaginaDeAdelantos>(
    `/adelantos${query({
      personaId: filtros.personaId,
      desde: filtros.desde,
      hasta: filtros.hasta,
      estado: filtros.estado,
      pagina,
    })}`,
  )
}

/** Las personas con algún adelanto, para el filtro del listado (FR-014). */
export function listarPersonasConAdelantos() {
  return peticion<PersonaResumen[]>('/adelantos/personas')
}

// ── Ciclo de vida ───────────────────────────────────────────────────────────────────────────────

/** Sin cuerpo y sin confirmación: se deshace con la anulación (research §4). */
export function aprobarAdelanto(id: number) {
  return peticion<AdelantoDetalle>(`/adelantos/${id}/aprobacion`, { metodo: 'POST' })
}

/**
 * Siempre con `confirmado: true`: **el diálogo es la confirmación explícita**. El `409` sigue ahí para
 * quien invoca la acción sin pasar por la pantalla (research §4, FR-024).
 */
export function rechazarAdelanto(id: number, motivo: string) {
  return peticion<AdelantoDetalle>(`/adelantos/${id}/rechazo`, {
    metodo: 'POST',
    cuerpo: { motivo, confirmado: true },
  })
}

/** Siempre con `confirmado: true`, por la misma razón que el rechazo (FR-029). */
export function anularAdelanto(id: number, motivo: string) {
  return peticion<AdelantoDetalle>(`/adelantos/${id}/anulacion`, {
    metodo: 'POST',
    cuerpo: { motivo, confirmado: true },
  })
}
