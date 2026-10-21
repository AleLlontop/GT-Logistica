import type { AlertaVencimiento } from '../../choferes/servicios/servicioChoferes'
import type { AlertaVencimientoFlota } from '../../flota/servicios/servicioFlota'
import type { FilaDeVencimiento } from '../../facturacion/servicios/servicioFacturas'
import type { ErrorApi } from '../../../compartido/tipos'

/** Contratos015 localizados: ninguna conversión de los DTO monetarios legacy. */
export type ImporteAnalisis = string
export interface PeriodoAnalisis { desde: string; hasta: string }
export interface PaginaAnalisis<T> { items: T[]; total: number; pagina: number; tamanioPagina: number }
export interface ReferenciaAnalisis { id: number; nombre: string; activo?: boolean | null }
export type CriterioTemporal = 'fechaViaje' | 'fechaFactura' | 'fechaCobro' | 'periodoLiquidado' | 'fechaPago' | 'fechaMovimientoArgentina' | 'situacionActual' | 'perspectivasSeparadas'
export interface ContextoAnalisis {
  desde: string | null; hasta: string | null; consultadoEn: string; hoyArgentina: string
  criterioTemporal: CriterioTemporal; periodoLiquidadoDesde?: string | null; periodoLiquidadoHasta?: string | null
}
export interface GrupoCantidad { id: string | null; nombre: string; cantidad: number }
export interface GrupoImporte extends GrupoCantidad { importe: ImporteAnalisis }
export interface Distribucion<T> { top: T[]; otros: T | null; totalCategorias: number }
export interface ResumenViajes { cantidad: number; realizados: number; pendientesRendicion: number; pendientes: number; anulados: number; importeTotal: ImporteAnalisis }
export interface MesViajes { periodo: string; cantidad: number; realizados: number; pendientesRendicion: number }
export interface DistribucionesViajes { estado: Distribucion<GrupoCantidad>; cliente: Distribucion<GrupoImporte>; transportista: Distribucion<GrupoImporte> }
export interface FilaViaje {
  id: number; numero: number; fecha: string; cliente: ReferenciaAnalisis; origen: string; destino: string
  chofer: ReferenciaAnalisis | null; vehiculo: ReferenciaAnalisis | null; transportista: ReferenciaAnalisis | null
  estado: 'pendiente' | 'enCurso' | 'rendido' | 'anulado' | 'facturado'; importe: ImporteAnalisis
}
export interface ResumenFacturacion { cantidad: number; facturado: ImporteAnalisis; cobradoVigenteDocumental: ImporteAnalisis; pendienteVigenteDocumental: ImporteAnalisis; cantidadAnuladas: number }
export interface MesFacturacion { periodo: string; facturado: ImporteAnalisis; cobradoVigenteDocumental: ImporteAnalisis; pendienteVigenteDocumental: ImporteAnalisis }
export interface DistribucionesFacturacion { estado: Distribucion<GrupoCantidad>; cliente: Distribucion<GrupoImporte> }
export interface FilaFactura {
  id: number; numeroComprobante: string; fecha: string; clienteId: number; clienteRazonSocial: string
  estado: 'pendiente' | 'vencida' | 'pagada' | 'anulada'; neto: ImporteAnalisis; iva: ImporteAnalisis; total: ImporteAnalisis; fechaCobro: string | null
}
export interface ResumenCobros { cantidadCobros: number; importeCobrado: ImporteAnalisis }
export interface MesCobros { periodo: string; importeCobrado: ImporteAnalisis }
export interface FilaCobro { facturaId: number; numeroComprobante: string; clienteId: number; clienteRazonSocial: string; fechaFactura: string; fechaCobro: string; importeCobrado: ImporteAnalisis }
export interface ResumenLiquidaciones { cantidad: number; importeLiquidado: ImporteAnalisis; pagadoVigenteDocumental: ImporteAnalisis; pendienteVigenteDocumental: ImporteAnalisis; cantidadAnuladas: number }
export interface MesLiquidaciones { periodo: string; importeLiquidado: ImporteAnalisis; pagadoVigenteDocumental: ImporteAnalisis; pendienteVigenteDocumental: ImporteAnalisis }
export interface FilaLiquidacion { id: number; numero: string; periodo: string; transportista: ReferenciaAnalisis; estado: 'pendiente' | 'pagada' | 'anulada'; importeTotal: ImporteAnalisis; pagadoVigente: ImporteAnalisis; pendienteVigente: ImporteAnalisis | null }
export interface ResumenPagos { cantidadPagosVigentes: number; importePagadoVigente: ImporteAnalisis; cantidadRevertidos: number; importeRevertidoHistorico: ImporteAnalisis }
export interface MesPagos { periodo: string; importePagadoVigente: ImporteAnalisis }
export interface FilaPago { id: number; numero: string; liquidacionId: number; liquidacionNumero: string; periodoLiquidado: string; transportista: ReferenciaAnalisis; fechaPago: string; importe: ImporteAnalisis; vigente: boolean; origen: 'manual' | 'opp'; oppId: number | null; oppNumero: string | null; revertidaEn: string | null; revertidaPor: ReferenciaAnalisis | null }
export interface ResumenCaja { cantidad: number; ingresos: ImporteAnalisis; egresos: ImporteAnalisis; neto: ImporteAnalisis }
export interface MesCaja { periodo: string; ingresos: ImporteAnalisis; egresos: ImporteAnalisis; neto: ImporteAnalisis }
export interface FilaMovimiento { id: number; cajaId: number; cajaNombre: string; fecha: string; tipo: 'ingreso' | 'egreso'; importe: ImporteAnalisis; concepto: string; responsable: ReferenciaAnalisis; referencia: string | null; ordenDePagoId: number | null; ordenPagoProveedorId: number | null }
export interface ResumenFlota { cantidadUnidades: number; totalViajesConVehiculo: number; sinVehiculoEnPeriodo: number }
export interface MesFlota { periodo: string; cantidadViajes: number }
export interface FilaFlota { id: number; patente: string; tipo: ReferenciaAnalisis; transportista: ReferenciaAnalisis; activo: boolean; estadoOperativoGuardado: 'disponible' | 'fueraDeServicio'; disponibilidadActual: 'disponible' | 'fueraDeServicio'; cantidadViajes: number }
export interface Analisis<R, M, D, F> { contexto: ContextoAnalisis; resumen: R; series: M[]; distribuciones: D; detalle: PaginaAnalisis<F> }
export type AnalisisViajes = Analisis<ResumenViajes, MesViajes, DistribucionesViajes, FilaViaje>
export type AnalisisFacturacion = Analisis<ResumenFacturacion, MesFacturacion, DistribucionesFacturacion, FilaFactura>
export type AnalisisCobros = Analisis<ResumenCobros, MesCobros, { cliente: Distribucion<GrupoImporte> }, FilaCobro>
export type AnalisisLiquidaciones = Analisis<ResumenLiquidaciones, MesLiquidaciones, { estado: Distribucion<GrupoCantidad>; transportista: Distribucion<GrupoImporte> }, FilaLiquidacion>
export type AnalisisPagos = Analisis<ResumenPagos, MesPagos, { transportista: Distribucion<GrupoImporte> }, FilaPago>
export type AnalisisCaja = Analisis<ResumenCaja, MesCaja, { tipo: Distribucion<GrupoImporte> }, FilaMovimiento>
export type AnalisisFlota = Analisis<ResumenFlota, MesFlota, { tipo: Distribucion<GrupoCantidad>; transportista: Distribucion<GrupoCantidad> }, FilaFlota>
export interface AnalisisVencimientos<T> { contexto: ContextoAnalisis; detalle: PaginaAnalisis<T> }
export type VencimientoFacturaAnalisis = Omit<FilaDeVencimiento, 'total'> & { total: ImporteAnalisis }
export interface RespuestasAnalisis {
  viajes: AnalisisViajes; facturacion: AnalisisFacturacion; 'facturacion/cobros': AnalisisCobros
  liquidaciones: AnalisisLiquidaciones; 'liquidaciones/pagos': AnalisisPagos; caja: AnalisisCaja; flota: AnalisisFlota
  'vencimientos/choferes': AnalisisVencimientos<AlertaVencimiento>
  'vencimientos/flota': AnalisisVencimientos<AlertaVencimientoFlota>
  'vencimientos/facturas': AnalisisVencimientos<VencimientoFacturaAnalisis> & { importeTotal: ImporteAnalisis }
}
export type RecursoAnalisis = keyof RespuestasAnalisis
export type RecursoTemporal = Exclude<RecursoAnalisis, `vencimientos/${string}`>
export type PanelVencimientos = Extract<RecursoAnalisis, `vencimientos/${string}`>
export interface FiltroPagina { pagina?: number; tamanioPagina?: number }
export interface FiltroBase extends PeriodoAnalisis, FiltroPagina {}
export interface FiltrosAnalisis {
  viajes: FiltroBase & { clienteId?: number; transportistaId?: number; estado?: FilaViaje['estado']; busqueda?: string }
  facturacion: FiltroBase & { clienteId?: number; estado?: FilaFactura['estado']; numero?: string }
  'facturacion/cobros': FiltroBase & { clienteId?: number; numero?: string }
  liquidaciones: FiltroBase & { transportistaId?: number; estado?: FilaLiquidacion['estado'] }
  'liquidaciones/pagos': FiltroBase & { transportistaId?: number; vigencia?: 'todos' | 'vigentes' | 'revertidos' }
  caja: FiltroBase & { cajaId?: number }
  flota: FiltroBase & { vehiculoId?: number; tipoVehiculoId?: number; transportistaId?: number; activo?: boolean; orden?: 'cantidadDesc' | 'cantidadAsc' }
  'vencimientos/choferes': FiltroPagina; 'vencimientos/flota': FiltroPagina; 'vencimientos/facturas': FiltroPagina
}
export interface DimensionesAnalisis { viajes: 'estado' | 'cliente' | 'transportista'; facturacion: 'estado' | 'cliente'; 'facturacion/cobros': 'cliente'; liquidaciones: 'estado' | 'transportista'; 'liquidaciones/pagos': 'transportista'; caja: 'tipo'; flota: 'tipo' | 'transportista' }
export interface GruposAnalisis { contexto: ContextoAnalisis; detalle: PaginaAnalisis<GrupoCantidad & { importe?: ImporteAnalisis }> }
export type SeccionDashboard<T> = { estado: 'disponible'; datos: T } | { estado: 'sinPermiso'; datos: null }
export interface DashboardAnalisis {
  contexto: ContextoAnalisis
  secciones: {
    viajes: SeccionDashboard<Pick<AnalisisViajes, 'resumen' | 'series' | 'distribuciones'>>
    facturacion: SeccionDashboard<{ documentos: ResumenFacturacion; serieDocumentos: MesFacturacion[]; cobros: ResumenCobros; serieCobros: MesCobros[]; distribucionesDocumentos: DistribucionesFacturacion; distribucionesCobros: { cliente: Distribucion<GrupoImporte> } }>
    liquidaciones: SeccionDashboard<{ documentos: ResumenLiquidaciones; serieDocumentos: MesLiquidaciones[]; pagos: ResumenPagos; seriePagos: MesPagos[]; transportista: Distribucion<GrupoImporte> }>
    caja: SeccionDashboard<{ resumen: ResumenCaja; series: MesCaja[] }>
    flota: SeccionDashboard<{ resumen: ResumenFlota; series: MesFlota[]; tipo: Distribucion<GrupoCantidad>; ranking: Distribucion<GrupoCantidad> }>
    vencimientosChoferes: SeccionDashboard<{ contexto: ContextoAnalisis; resumen: { cantidad: number } }>
    vencimientosFlota: SeccionDashboard<{ contexto: ContextoAnalisis; resumen: { cantidad: number } }>
    vencimientosFacturas: SeccionDashboard<{ contexto: ContextoAnalisis; resumen: { cantidad: number; importeTotal: ImporteAnalisis } }>
  }
}
export interface ErrorAnalisis extends ErrorApi { campos?: Record<string, string[]> }
