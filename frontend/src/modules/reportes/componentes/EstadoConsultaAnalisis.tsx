import { ErrorHttp } from '../../../compartido/clienteHttp'
import { Boton } from '../../../compartido/ui/Boton'
import { EstadoVacio } from '../../../compartido/ui/EstadoVacio'
import type { SolicitudAnalisis } from '../servicios/useSolicitudAnalisis'

export function SinPermisoReporte() {
  return <EstadoVacio caso="error" anuncioEnMensaje>No tenés permiso para consultar este reporte. Pedíselo a quien administra el sistema.</EstadoVacio>
}
export function SinDatosReporte({ mensaje = 'Sin datos para el período.' }: { mensaje?: string }) {
  return <EstadoVacio caso="sinCoincidencias" anuncioEnMensaje>{mensaje}</EstadoVacio>
}
export function EstadoConsultaAnalisis<T>({ solicitud }: { solicitud: SolicitudAnalisis<T> }) {
  if (solicitud.estado === 'listo') return null
  if (solicitud.estado === 'inactivo') return <SinPermisoReporte />
  if (solicitud.estado === 'cargando') return <EstadoVacio caso="cargando" anuncioEnMensaje>Cargando análisis…</EstadoVacio>
  const error = solicitud.error
  const mensaje = error instanceof ErrorHttp ? error.detalle.mensaje : 'No pudimos cargar el análisis. Volvé a intentar.'
  const sinReintento = error instanceof ErrorHttp && (error.estado === 401 || error.estado === 403)
  return <EstadoVacio caso="error" anuncioEnMensaje accion={sinReintento ? undefined : <Boton variante="secundario" onClick={solicitud.reintentar}>Reintentar</Boton>}>{mensaje}</EstadoVacio>
}
