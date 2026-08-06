import type { ReactNode } from 'react'
import { Navigate, useLocation } from 'react-router-dom'
import type { Sesion } from '../servicios/sesion'

interface Props {
  sesion: Sesion | null
  children: ReactNode
}

/**
 * Exige sesión activa para llegar a cualquier pantalla que no sea la de ingreso (FR-007).
 *
 * Recuerda la ruta pedida para poder volver a ella después de autenticarse (FR-026).
 *
 * Esto es comodidad, no protección: ocultar o bloquear una pantalla en el cliente no protege nada.
 * El backend verifica la autorización en cada operación, sin importar lo que muestre el menú
 * (FR-008).
 */
export function RutaProtegida({ sesion, children }: Props) {
  const ubicacion = useLocation()

  if (sesion === null) {
    const destino = `${ubicacion.pathname}${ubicacion.search}`

    return <Navigate to="/ingresar" state={{ destino }} replace />
  }

  return <>{children}</>
}
