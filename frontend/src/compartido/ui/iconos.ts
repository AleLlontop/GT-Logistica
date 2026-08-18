/**
 * Los únicos íconos del sistema.
 *
 * Se reexportan desde acá —y no se importan de `lucide-react` en cada pantalla— para que la lista
 * de lo que usamos esté en un solo lugar y el empaquetado descarte el resto.
 *
 * Tres reglas que valen para todos (FR-005, FR-040):
 *
 * 1. **Ningún ícono comunica solo.** Siempre acompaña a una palabra que ya está en pantalla.
 * 2. Van marcados como decorativos —`aria-hidden`—, para que un lector de pantalla no lea dos veces
 *    lo mismo. Las primitivas que los usan ya lo hacen.
 * 3. Heredan el color del texto: no se les asigna un color propio fuera de la paleta.
 */
export {
  // Secciones de navegación
  Truck as IconoOperacion,
  Users as IconoPadrones,
  ChartColumn as IconoSeguimiento,
  Settings as IconoConfiguracion,
  ShieldCheck as IconoAdministracion,

  // Estados
  CircleCheck as IconoEnRegla,
  TriangleAlert as IconoProximoAvencer,
  CircleAlert as IconoVencido,
  Clock as IconoPendiente,
  Ban as IconoAnulado,

  // Acciones
  Plus as IconoNuevo,
  Pencil as IconoEditar,
  Trash2 as IconoEliminar,
  Search as IconoBuscar,
  FileText as IconoDocumento,
  ArrowLeft as IconoVolver,
  LogOut as IconoCerrarSesion,
  KeyRound as IconoContrasena,
  X as IconoCerrar,

  // Paginación
  ChevronLeft as IconoAnterior,
  ChevronRight as IconoSiguiente,
} from 'lucide-react'
