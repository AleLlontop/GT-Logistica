import { describe, expect, it } from 'vitest'
import { formatearFecha, formatearInstante } from './fechas'

describe('formatearFecha', () => {
  /**
   * El error que motivó este módulo: `new Date('1985-03-12')` interpreta la fecha como medianoche
   * UTC y, en hora de Argentina, retrocede un día. Quien nació el 12 figuraba nacido el 11.
   */
  it('no corre la fecha un día hacia atrás (bug del padrón del Módulo 2)', () => {
    expect(formatearFecha('1985-03-12')).toBe('12/03/1985')
    expect(formatearFecha('1980-07-20')).toBe('20/07/1980')
  })

  /** El borde que lo hacía visible: el primer día del año, con UTC−3, caía en el 31 de diciembre. */
  it('respeta el primero de enero', () => {
    expect(formatearFecha('2026-01-01')).toBe('01/01/2026')
  })

  it('completa con ceros el día y el mes', () => {
    expect(formatearFecha('2026-08-06')).toBe('06/08/2026')
  })

  it('acepta también un instante y muestra su día', () => {
    expect(formatearFecha('2026-08-06T15:30:00Z')).toMatch(/^0[56]\/08\/2026$/)
  })
})

describe('formatearInstante', () => {
  it('muestra la fecha con la hora', () => {
    expect(formatearInstante('2026-08-06T15:30:00Z')).toMatch(/^\d{2}\/\d{2}\/2026 \d{2}:\d{2}$/)
  })
})
