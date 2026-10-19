import { useState, type ReactNode } from 'react'
import type { DetalleFacturaProveedor } from '../servicios/tipos'
import { ContextoResultadoFactura } from './resultadoFactura'

/** Sólo el último POST, durante este runtime. El key de sesión en App descarta el estado al salir/cambiar usuario. */
export function ResultadoFacturaProvider({ children }: { children: ReactNode }) {
  const [resultado, guardar] = useState<DetalleFacturaProveedor | null>(null)
  return <ContextoResultadoFactura.Provider value={{ resultado, guardar, invalidar: ids => guardar(actual => actual && ids.includes(actual.id) ? null : actual) }}>{children}</ContextoResultadoFactura.Provider>
}
