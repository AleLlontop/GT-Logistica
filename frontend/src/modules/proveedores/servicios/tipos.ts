/** Importe canónico HTTP: punto y exactamente dos decimales. Nunca se convierte completo a number. */
export type ImporteDecimal = string & { readonly __importeDecimal: unique symbol }
export type TipoProveedor = 'fletero' | 'proveedorNormal'
export type EstadoProveedor = 'activo' | 'inactivo'
export interface Pagina<T> { items: T[]; total: number; pagina: number; tamanioPagina: number }
export interface ProveedorResumen {
  id: number
  razonSocial: string
  nombreComercial: string | null
  cuit: string
  tipo: TipoProveedor
  estado: EstadoProveedor
}
export interface ProveedorDetalle extends ProveedorResumen {
  telefono: string | null
  email: string | null
  direccion: string | null
  transportistaId: number | null
  version: number
}
export interface ProveedorPeticion {
  tipo: TipoProveedor
  razonSocial?: string
  cuit?: string
  telefono?: string
  email?: string
  nombreComercial?: string
  direccion?: string
  transportistaId?: number
  version?: number
}
export interface TransportistaExterno { id: number; razonSocial: string; cuit: string }
export interface LiquidacionDisponible {
  id: number
  numero: string
  periodo: string
  importeTotal: ImporteDecimal
}
export interface FacturaProveedorResumen {
  id: number
  numero: string
  fecha: string
  proveedorId: number
  proveedorRazonSocial: string
  proveedorCuit: string
  importeTotal: ImporteDecimal
  estado: 'pendiente' | 'pagada'
}
export interface FiltrosFacturaProveedor {
  desde?: string
  hasta?: string
  proveedorId?: number
  estado?: 'pendiente' | 'pagada'
}
export interface DetalleFacturaProveedor extends FacturaProveedorResumen {
  tipoProveedorAlRegistrar: TipoProveedor
  detalle: string
  neto: ImporteDecimal
  iva: ImporteDecimal
  cae: string
  fechaVencimientoCae: string
  archivo: { nombre: string; url: string } | null
  liquidacion: {
    id: number
    numero: string
    periodo: string
    importeAlRegistrar: ImporteDecimal
    importeActual: ImporteDecimal
    estadoActual: 'pendiente' | 'pagada' | 'anulada'
  } | null
}
export interface FacturaProveedorPeticion {
  proveedorId: number
  numero: string
  fecha: string
  detalle: string
  neto: ImporteDecimal
  cae: string
  fechaVencimientoCae: string
  liquidacionId?: number
  archivo?: File
}
export interface ConflictoImportes {
  codigo: string
  mensaje: string
  campo?: string
  importeTotalFactura: ImporteDecimal
  importeTotalLiquidacion: ImporteDecimal
}
