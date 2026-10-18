import { describe, expect, it } from 'vitest'
import { formatearPesos } from './moneda'

describe('formatearPesos', () => {
  /** El formato exacto que fija el Principio II: punto de miles, coma decimal, dos decimales. */
  it('usa punto de miles y coma decimal', () => {
    expect(formatearPesos(1240000)).toBe('$ 1.240.000,00')
  })

  it('muestra el cero como importe y no como vacío', () => {
    // Un viaje sin cargo es válido (FR-013). En blanco se confundiría con un dato sin cargar.
    expect(formatearPesos(0)).toBe('$ 0,00')
  })

  it('conserva siempre los dos decimales', () => {
    expect(formatearPesos(1500.5)).toBe('$ 1.500,50')
    expect(formatearPesos(99)).toBe('$ 99,00')
  })

  it('no separa los miles por debajo de mil', () => {
    expect(formatearPesos(999.99)).toBe('$ 999,99')
  })

  it('redondea a dos decimales', () => {
    expect(formatearPesos(1234.567)).toBe('$ 1.234,57')
  })
})
// 013: cadenas monetarias exactas; los casos anteriores para number se conservan.
it('conserva los centavos de cadenas grandes sin convertir a number', () => {
  expect(formatearPesos('100000000000000.01')).toBe('$ 100.000.000.000.000,01')
  expect(formatearPesos('100000000000000.02')).toBe('$ 100.000.000.000.000,02')
})
