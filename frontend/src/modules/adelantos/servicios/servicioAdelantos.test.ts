import { describe, expect, it } from 'vitest'
import { primeraFechaAdmitida } from './servicioAdelantos'

describe('primeraFechaAdmitida', () => {
  it('es el primer día del mes anterior', () => {
    expect(primeraFechaAdmitida(new Date(2026, 8, 14))).toBe('2026-08-01')
  })

  /** El piso cruza el año: el 1 de enero admite diciembre (spec §Edge Cases). */
  it('cruza el año el 1 de enero', () => {
    expect(primeraFechaAdmitida(new Date(2027, 0, 1))).toBe('2026-12-01')
  })

  /** Restar un mes al 31 de marzo no cae en un día que febrero no tiene. */
  it('no se corre desde el último día de un mes largo', () => {
    expect(primeraFechaAdmitida(new Date(2026, 2, 31))).toBe('2026-02-01')
  })
})
