import { entregarArchivo, obtenerArchivo, type EntregaRealizada } from '../../../compartido/archivos'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import type { RecursoAnalisis } from './tiposAnalisis'

/**
 * Los cinco reportes del sistema (contracts §Endpoints).
 *
 * **No hay una pantalla de reportes**: hay una acción sobre cinco pantallas que ya existen, y esto es
 * lo que la ejecuta.
 */

/** Cuál de los cinco. Decide la ruta, y nada más: el título y las columnas los pone el backend. */
export type TipoDeReporte =
  | 'viajes'
  | 'vencimientos-choferes'
  | 'vencimientos-flota'
  | 'vencimientos-facturas'
  | 'movimientos-caja'
  | 'analisis-viajes'
  | 'analisis-facturacion'
  | 'analisis-cobros'
  | 'analisis-liquidaciones'
  | 'analisis-pagos'
  | 'analisis-caja'
  | 'analisis-flota'
  | 'analisis-vencimientos-choferes'
  | 'analisis-vencimientos-flota'
  | 'analisis-vencimientos-facturas'

export type FormatoDeReporte = 'pdf' | 'excel'

/** Los filtros aplicados en la pantalla, ya como pares para la query. */
export type FiltrosDeReporte = Record<string, string | number | boolean | undefined | null>

const RUTAS: Record<TipoDeReporte, string> = {
  viajes: '/viajes/reporte',
  'vencimientos-choferes': '/vencimientos/reporte',
  'vencimientos-flota': '/flota/vencimientos/reporte',
  'vencimientos-facturas': '/facturas/vencimientos/reporte',
  'movimientos-caja': '/movimientos-caja/reporte',
  'analisis-viajes': '/reportes/viajes/exportacion',
  'analisis-facturacion': '/reportes/facturacion/exportacion',
  'analisis-cobros': '/reportes/facturacion/cobros/exportacion',
  'analisis-liquidaciones': '/reportes/liquidaciones/exportacion',
  'analisis-pagos': '/reportes/liquidaciones/pagos/exportacion',
  'analisis-caja': '/reportes/caja/exportacion',
  'analisis-flota': '/reportes/flota/exportacion',
  'analisis-vencimientos-choferes': '/reportes/vencimientos/choferes/exportacion',
  'analisis-vencimientos-flota': '/reportes/vencimientos/flota/exportacion',
  'analisis-vencimientos-facturas': '/reportes/vencimientos/facturas/exportacion',
}

const TIPOS_ANALISIS: Record<RecursoAnalisis, TipoDeReporte> = {
  viajes: 'analisis-viajes', facturacion: 'analisis-facturacion', 'facturacion/cobros': 'analisis-cobros',
  liquidaciones: 'analisis-liquidaciones', 'liquidaciones/pagos': 'analisis-pagos', caja: 'analisis-caja', flota: 'analisis-flota',
  'vencimientos/choferes': 'analisis-vencimientos-choferes', 'vencimientos/flota': 'analisis-vencimientos-flota', 'vencimientos/facturas': 'analisis-vencimientos-facturas',
}
export function tipoDeReporteAnalisis(recurso: RecursoAnalisis): TipoDeReporte { return TIPOS_ANALISIS[recurso] }

/**
 * Lo que devuelve generar un reporte.
 *
 * **El `409` del tope vuelve como resultado y no como excepción** (convención [009], contracts §El
 * 409 vuelve como resultado): no es un fallo, es una respuesta esperada que la pantalla muestra.
 *
 * El `400` y el `500`, en cambio, vuelven como error: la pantalla los muestra con el `codigo` y el
 * `mensaje` que llegan del cuerpo, **nunca con un texto compuesto acá** (research §4).
 */
export type ResultadoDeReporte =
  | { estado: 'entregado'; entrega: EntregaRealizada }
  | { estado: 'tope_superado'; mensaje: string; filas: number; tope: number }
  | { estado: 'precision_monetaria_xlsx'; mensaje: string }

/** El cuerpo del `409`, que trae los dos números además del mensaje ya armado. */
interface ErrorDeTope {
  codigo: string
  mensaje: string
  filas: number
  tope: number
}

/**
 * Pide el reporte y lo entrega: el PDF se abre en una pestaña y el Excel se descarga.
 *
 * **`pagina` no se manda nunca**: el reporte abarca todas las filas del filtro, no las de la página
 * visible (FR-007).
 *
 * **El tope de 5.000 no se pre-verifica acá** aunque la pantalla conozca el total del listado: el
 * mensaje que lo nombra lo arma el servidor y viaja en el `409`. Escribirlo también en TypeScript
 * serían dos textos que se pueden separar (research §4).
 */
export async function generarReporte(
  reporte: TipoDeReporte,
  formato: FormatoDeReporte,
  filtros: FiltrosDeReporte = {},
): Promise<ResultadoDeReporte> {
  try {
    const archivo = await obtenerArchivo(`${RUTAS[reporte]}${query({ ...filtros, formato })}`)

    return {
      estado: 'entregado',
      // `Content-Disposition` ya declara `inline` o `attachment`; acá se decide qué hacer con el
      // blob que ya está en memoria, y el criterio es el mismo: el PDF se mira, la planilla se baja.
      entrega: entregarArchivo(archivo, formato === 'pdf' ? 'abrir' : 'descargar'),
    }
  } catch (fallo) {
    if (fallo instanceof ErrorHttp && fallo.detalle.codigo === 'precision_monetaria_xlsx') {
      return { estado: 'precision_monetaria_xlsx', mensaje: fallo.detalle.mensaje }
    }
    if (fallo instanceof ErrorHttp && fallo.detalle.codigo === 'tope_de_filas_superado') {
      const detalle = fallo.detalle as unknown as ErrorDeTope

      return {
        estado: 'tope_superado',
        mensaje: detalle.mensaje,
        filas: detalle.filas,
        tope: detalle.tope,
      }
    }

    throw fallo
  }
}

/**
 * El mensaje de un rechazo, **tal como llega del servidor**.
 *
 * Un fallo de red no trae cuerpo que leer, así que ahí —y sólo ahí— se usa el texto genérico de
 * `reporte_no_generado`.
 */
export function mensajeDelRechazo(fallo: unknown): string {
  if (fallo instanceof ErrorHttp && fallo.detalle.mensaje !== '') {
    return fallo.detalle.mensaje
  }

  return 'No pudimos generar el reporte. Volvé a intentar en unos minutos.'
}

/** Arma la query descartando los parámetros vacíos, para no mandar `?clienteId=` sin valor. */
function query(parametros: FiltrosDeReporte) {
  const partes = new URLSearchParams()

  for (const [nombre, valor] of Object.entries(parametros)) {
    if (valor !== '' && valor !== undefined && valor !== null && nombre !== 'pagina' && nombre !== 'tamanioPagina') {
      partes.append(nombre, String(valor))
    }
  }

  return `?${partes.toString()}`
}
