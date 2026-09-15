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

/**
 * Una fecha en `yyyy-MM-dd`, el formato con el que viaja al backend.
 *
 * Con el año, el mes y el día **locales** y ceros a la izquierda, **sin pasar por `toISOString()`**: eso
 * convierte a UTC, y a las 22 en Argentina ya es el día siguiente. Tres pantallas de los Módulos 6 y 9
 * tenían su propia copia de esto; el Módulo 10 fue la cuarta necesidad (research §10b del Módulo 10).
 */
export function enIso(fecha: Date): string {
  return `${fecha.getFullYear()}-${String(fecha.getMonth() + 1).padStart(2, '0')}-${String(
    fecha.getDate(),
  ).padStart(2, '0')}`
}

/** Hoy en `yyyy-MM-dd`, el día local de quien usa la pantalla. */
export function hoyEnIso(): string {
  return enIso(new Date())
}

/** El primer año con viajes cargados en el sistema. */
const PRIMER_ANIO = 2025

/**
 * Los años que se pueden elegir como período de una factura o de una liquidación: de 2025 al en curso.
 *
 * Se derivan de hoy para que el año nuevo aparezca solo, y el anterior sigue para facturar o liquidar
 * diciembre en enero. El servidor aplica la misma regla y es quien la garantiza.
 */
export function aniosDelPeriodo(hoy: Date = new Date()): number[] {
  return Array.from({ length: hoy.getFullYear() - PRIMER_ANIO + 1 }, (_, indice) => PRIMER_ANIO + indice)
}
