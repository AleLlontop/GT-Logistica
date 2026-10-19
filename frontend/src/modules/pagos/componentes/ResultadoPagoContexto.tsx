import { useContext, useState, type ReactNode } from 'react'
import type { Banco, DetalleOrden } from '../servicios/tipos'
import { ContextoResultadoFactura } from '../../proveedores/componentes/resultadoFactura'
import { ContextoResultadoPago } from './resultadoPago'
/** App monta este provider con la key de sesión: sólo runtime, se descarta al salir o cambiar usuario. */
export function ResultadoPagoProvider({ children }: { children: ReactNode }) {
  const facturas = useContext(ContextoResultadoFactura)
  const [orden, setOrden] = useState<DetalleOrden | null>(null)
  const [banco, guardarBanco] = useState<Banco | null>(null)
  return <ContextoResultadoPago.Provider value={{ orden, guardarOrden: dto => { facturas?.invalidar(dto.facturas.map(f => f.id)); setOrden(dto) }, descartarOrden: id => setOrden(actual => actual?.id === id ? null : actual), banco, guardarBanco }}>{children}</ContextoResultadoPago.Provider>
}
