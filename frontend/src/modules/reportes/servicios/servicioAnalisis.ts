import { peticion } from '../../../compartido/clienteHttp'
import type { DashboardAnalisis, DimensionesAnalisis, FiltrosAnalisis, GruposAnalisis, PaginaAnalisis, PeriodoAnalisis, RecursoAnalisis, RecursoTemporal, ReferenciaAnalisis, RespuestasAnalisis } from './tiposAnalisis'

/** No transforma valores de respuesta: las cadenas monetarias exactas llegan intactas. */
export function consultarAnalisis<R extends RecursoAnalisis>(recurso: R, filtros: FiltrosAnalisis[R]): Promise<RespuestasAnalisis[R]> {
  return peticion(`/reportes/${recurso}${queryAnalisis(filtros)}`)
}
export function consultarDashboard(periodo: PeriodoAnalisis): Promise<DashboardAnalisis> {
  return peticion(`/reportes/dashboard${queryAnalisis(periodo)}`)
}
export function consultarGrupos<R extends RecursoTemporal>(recurso: R, dimension: DimensionesAnalisis[R], filtros: FiltrosAnalisis[R]): Promise<GruposAnalisis> {
  return peticion(`/reportes/${recurso}/grupos${queryAnalisis({ ...filtros, dimension })}`)
}
export const RUTAS_REFERENCIAS = {
  clientesViajes: 'viajes/referencias/clientes', transportistasViajes: 'viajes/referencias/transportistas',
  clientesFacturacion: 'facturacion/referencias/clientes', transportistasLiquidaciones: 'liquidaciones/referencias/transportistas',
  cajas: 'caja/referencias/cajas', vehiculos: 'flota/referencias/vehiculos', tiposVehiculo: 'flota/referencias/tipos', transportistasFlota: 'flota/referencias/transportistas',
} as const
export type TipoReferenciaAnalisis = keyof typeof RUTAS_REFERENCIAS
export function consultarReferencias(referencia: TipoReferenciaAnalisis, busqueda?: string, pagina?: number): Promise<PaginaAnalisis<ReferenciaAnalisis>> {
  return peticion(`/reportes/${RUTAS_REFERENCIAS[referencia]}${queryAnalisis({ busqueda, pagina })}`)
}
export function queryAnalisis(filtros: object): string {
  const query = new URLSearchParams()
  for (const [clave, valor] of Object.entries(filtros)) {
    if (valor !== undefined && valor !== null && valor !== '') query.set(clave, String(valor))
  }
  const texto = query.toString()
  return texto ? `?${texto}` : ''
}

/** Lectura exacta para presentar/coordenadas; sin tope de formulario ni sumas autoritativas. */
export function centavosAnalisis(valor: string): bigint {
  if (!/^-?(0|[1-9]\d*)\.\d{2}$/.test(valor) || valor === '-0.00') throw new Error('Importe monetario inválido.')
  return BigInt(valor.replace('.', ''))
}
