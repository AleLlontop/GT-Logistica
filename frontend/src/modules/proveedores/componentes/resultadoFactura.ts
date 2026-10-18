import { createContext, useContext } from 'react'
import type { DetalleFacturaProveedor } from '../servicios/tipos'

export const ContextoResultadoFactura = createContext<{ resultado: DetalleFacturaProveedor | null; guardar: (dto: DetalleFacturaProveedor) => void } | null>(null)
export function useResultadoFactura() {
  const contexto = useContext(ContextoResultadoFactura)
  if (!contexto) throw new Error('Falta el contexto de resultado de factura.')
  return contexto
}
