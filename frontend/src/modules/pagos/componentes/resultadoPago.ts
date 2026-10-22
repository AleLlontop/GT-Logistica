import { createContext, useContext } from 'react'
import type { Banco, DetalleOrden } from '../servicios/tipos'
export const ContextoResultadoPago = createContext<{
  orden: DetalleOrden | null; guardarOrden: (orden: DetalleOrden) => void; descartarOrden: (id: number) => void
  banco: Banco | null; guardarBanco: (banco: Banco) => void
} | null>(null)
export function useResultadoPago() {
  const contexto = useContext(ContextoResultadoPago)
  if (!contexto) throw new Error('Falta el contexto de resultado de pagos.')
  return contexto
}
