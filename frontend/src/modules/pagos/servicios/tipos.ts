import type { ImporteDecimal, Pagina, TipoProveedor } from '../../proveedores/servicios/tipos'
export type { ImporteDecimal, Pagina, TipoProveedor }
export type EstadoOrden = 'pendiente' | 'anulada'
export type EstadoCheque = 'pendiente' | 'entregado' | 'depositado' | 'cobrado' | 'rechazado' | 'anulado'
export type SituacionCheque = 'vencido' | 'proximoAvencer' | 'fueraDelPeriodoDeAlerta'
export interface Banco { id: number; nombre: string; activo: boolean; version: number }
export type BancoReferencia = Omit<Banco, 'version'>
export interface ProveedorOpcion { id: number; razonSocial: string; cuit: string; tipo: TipoProveedor; activo: boolean }
export interface Referencia { id: number; numero: string }
export interface UsuarioPago { id: number; nombre: string }
export interface LiquidacionElegible extends Referencia {
  importeHistorico: ImporteDecimal; importeActual: ImporteDecimal; saldoActual: ImporteDecimal; estado: string
}
export interface FacturaElegible extends Referencia {
  fecha: string; importeTotal: ImporteDecimal; proveedorId: number; tipoProveedorAlRegistrar: TipoProveedor
  liquidacion: LiquidacionElegible | null
}
export interface SeleccionFacturas { items: FacturaElegible[]; noDisponibles: number[]; importeTotal: ImporteDecimal }
export interface ReferenciasPago {
  hoyArgentina: string
  cajaPropia: { id: number; estado: string; saldoDisponible: string /* Saldo de Caja puede ser negativo; no es un importe de medio. */ } | null
  bancosActivos: BancoReferencia[]
}
interface MedioBase { renglonId: string; importe: ImporteDecimal }
export interface ChequeEntrada extends MedioBase {
  tipo: 'cheque'; bancoId: number; numeroCheque: string; fechaEmision: string; fechaVencimiento: string; detalle?: string | null
}
export interface TransferenciaEntrada extends MedioBase {
  tipo: 'transferencia'; fecha: string; cbuOrigen: string; cbuDestino: string; numeroComprobante: string; observaciones?: string | null
}
export interface EfectivoEntrada extends MedioBase { tipo: 'efectivo' }
export type MedioEntrada = ChequeEntrada | TransferenciaEntrada | EfectivoEntrada
export interface GenerarEntrada {
  claveOperacion: string; proveedorId: number; fecha: string; detalle?: string | null; facturaIds: number[]
  medios: MedioEntrada[]; importeTotalConfirmado: ImporteDecimal; confirmado: true
}
export interface AnularEntrada { claveOperacion: string; motivo: string; confirmado: true }
export interface ProveedorEnOrden {
  id: number; razonSocial: string; cuit: string; tipo: TipoProveedor; activoAlGenerar: boolean; activoActual: boolean
}
export interface OrdenResumen extends Referencia {
  fecha: string; proveedor: ProveedorEnOrden; importeTotal: ImporteDecimal; estado: EstadoOrden
}
export interface PagoLiquidacion extends Referencia {
  fechaPago: string; importe: ImporteDecimal; vigente: boolean; revertidaEn: string | null
}
export interface FacturaEnOrden extends Referencia {
  fecha: string; importeTotalAlPagar: ImporteDecimal; estadoActual: string; vinculoVigente: boolean
  proveedorRazonSocial: string; proveedorCuit: string
  liquidacion: (Omit<LiquidacionElegible, 'estado'> & { estadoActual: string; pago: PagoLiquidacion }) | null
}
export interface MovimientoReferencia {
  id: number; cajaId: number; tipo: 'ingreso' | 'egreso'; importe: ImporteDecimal; concepto: string
  usuario: UsuarioPago; fecha: string; ordenPagoProveedor: Referencia
}
export type MedioDetalle =
  | (ChequeEntrada & { id: number; fecha: string; estado: EstadoCheque; situacionVencimiento: SituacionCheque; banco: BancoReferencia })
  | (TransferenciaEntrada & { id: number })
  | (EfectivoEntrada & { id: number; fecha: string })
export interface DetalleOrden extends OrdenResumen {
  detalle: string | null; registradaPor: UsuarioPago; registradaEn: string; facturas: FacturaEnOrden[]; medios: MedioDetalle[]
  caja: { id: number; egreso: MovimientoReferencia; ingresoCompensatorio: MovimientoReferencia | null } | null
  anulacion: {
    motivo: string; usuario: UsuarioPago; ocurridaEn: string
    efectos: { facturasRestituidas: number[]; chequesAnulados: number[]; pagosRevertidos: Referencia[]; movimientoCompensatorio: MovimientoReferencia | null }
  } | null
}
export interface ChequeResumen {
  id: number; numeroCheque: string; banco: BancoReferencia; fechaEmision: string; fechaVencimiento: string
  importe: ImporteDecimal; estado: EstadoCheque; situacionVencimiento: SituacionCheque; ordenPagoProveedor: Referencia
}
export interface DetalleCheque extends ChequeResumen {
  detalle: string | null
  historial: { id: number; estadoAnterior: EstadoCheque; estadoNuevo: EstadoCheque; usuario: UsuarioPago; ocurridoEn: string }[]
}
export interface FiltrosOrdenes { desde?: string; hasta?: string; proveedorId?: number; estado?: EstadoOrden | ''; pagina: number }
export interface FiltrosCheques { bancoId?: number; estado?: EstadoCheque | ''; desdeVencimiento?: string; hastaVencimiento?: string; pagina: number }
