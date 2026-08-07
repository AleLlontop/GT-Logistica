import { format, parseISO } from 'date-fns'

/**
 * Formateo de fechas del sistema, en un solo lugar.
 *
 * Existe por un error concreto: `new Date('1985-03-12')` interpreta la fecha como **medianoche
 * UTC**, y al mostrarla en hora de Argentina (UTC−3) retrocede un día. Una persona nacida el 12 de
 * marzo figuraba nacida el 11. El padrón del Módulo 2 lo tuvo hasta que el recorrido del quickstart
 * del Módulo 3 lo hizo visible.
 *
 * `parseISO` de date-fns distingue las dos formas que devuelve el backend, que es justo lo que hace
 * falta acá:
 *
 * - `yyyy-MM-dd` (un `DateOnly`: nacimiento, emisión, vencimiento) → **no lleva zona horaria**, así
 *   que se interpreta como ese día, sin corrimiento posible.
 * - `yyyy-MM-ddTHH:mm:ssZ` (un `DateTime`: alta de la cuenta, último acceso) → es un instante real y
 *   se muestra en la hora local de quien mira.
 */

const FORMATO_FECHA = 'dd/MM/yyyy'
const FORMATO_INSTANTE = 'dd/MM/yyyy HH:mm'

/**
 * Una fecha sin hora, como se lee acá: `12/03/1985`.
 *
 * Pensada para los `DateOnly` del backend. Si le llega un instante, muestra su día en hora local.
 */
export function formatearFecha(iso: string): string {
  return format(parseISO(iso), FORMATO_FECHA)
}

/** Un instante con su hora: `06/08/2026 14:30`. */
export function formatearInstante(iso: string): string {
  return format(parseISO(iso), FORMATO_INSTANTE)
}
