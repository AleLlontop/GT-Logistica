import { Building2, CalendarClock, FileText, Landmark, Package, Route, Settings, ShieldCheck, Truck, Users, Wallet, ChartNoAxesCombined, type LucideIcon } from 'lucide-react'

/** Decoración, nunca autorización: también se conserva un ícono para las rutas desconocidas. */
export function iconoDeRuta(ruta: string): LucideIcon {
  if (ruta.includes('vencimientos') || ruta.includes('cheques')) return CalendarClock
  if (ruta.includes('reportes') || ruta.includes('totales')) return ChartNoAxesCombined
  if (ruta.includes('bancos')) return Landmark
  if (ruta.includes('usuarios')) return ShieldCheck
  if (ruta.includes('tipos') || ruta.includes('empresa')) return Settings
  if (ruta.includes('clientes') || ruta.includes('proveedores')) return Building2
  if (ruta.includes('choferes') || ruta.includes('personas') || ruta.includes('empleados')) return Users
  if (ruta.includes('flota') || ruta.includes('transportistas')) return Truck
  if (ruta.includes('viajes')) return Route
  if (ruta.includes('caja') || ruta.includes('pagos') || ruta.includes('adelantos')) return Wallet
  if (ruta.includes('facturas') || ruta.includes('liquidaciones')) return FileText
  return Package
}
