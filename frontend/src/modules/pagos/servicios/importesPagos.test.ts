import { describe, expect, it } from 'vitest'
import { centavos, importeCanonico, sumarImportes } from './importesPagos'
import type { ImporteDecimal } from './tipos'
const importe = (valor: string) => valor as ImporteDecimal
describe('Importes exactos de pagos', () => {
  it('conserva el último centavo de decimal(18,2)', () => {
    expect(importeCanonico('9.999.999.999.999.999,99')).toBe('9999999999999999.99')
    expect(centavos(importe('9999999999999999.99'))).toBe(999999999999999999n)
    expect(sumarImportes([importe('9999999999999999.98'), importe('0.01')])).toBe('9999999999999999.99')
    expect(sumarImportes([importe('9999999999999999.99'), importe('0.01')])).toBeNull()
  })
  it('suma medios sin recalcular impuestos ni redondear diferencias', () => {
    expect(sumarImportes([importe('60.00'), importe('50.00')])).toBe('110.00')
    expect(sumarImportes([importe('600.00'), importe('250.00'), importe('150.00')])).toBe('1000.00')
    expect(sumarImportes([])).toBe('0.00')
    expect(sumarImportes([importe('1.001')])).toBeNull()
    expect(importeCanonico('0,00')).toBeNull()
  })
})
