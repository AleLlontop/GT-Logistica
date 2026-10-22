import { describe, expect, it } from 'vitest'
import { coordenadasGrafico } from './coordenadasGrafico'

describe('coordenadas de dibujo, nunca cálculo monetario autoritativo', () => {
  it('acota Number sólo después de escalar BigInt y conserva originales con centavos', () => {
    const datos = [
      { etiqueta: 'Octubre', valores: { cobros: '9999999999999999.99', pagos: '-19999999999999999.98' } },
      { etiqueta: 'Noviembre', valores: { cobros: '0.01', pagos: '0.00' } },
    ]
    const antes = JSON.stringify(datos)
    const modelo = coordenadasGrafico(datos, [{ clave: 'cobros', nombre: 'Cobros' }, { clave: 'pagos', nombre: 'Pagos' }], true)
    expect(modelo.factorPesos).toBeGreaterThan(1n)
    expect(modelo.unidad).toContain('ARS')
    expect(modelo.coordenadas[0].original).toBe(datos[0])
    expect(modelo.coordenadas[1].original.valores.cobros).toBe('0.01')
    for (const fila of modelo.coordenadas) for (const clave of ['cobros', 'pagos']) {
      expect(typeof fila[clave]).toBe('number')
      expect(Math.abs(fila[clave] as number)).toBeLessThanOrEqual(1000)
    }
    expect(JSON.stringify(datos)).toBe(antes)
  })
  it('cuenta cantidades sin convertirlas en importes y admite cero', () => {
    const modelo = coordenadasGrafico([{ etiqueta: 'A', valores: { viajes: 0 } }, { etiqueta: 'B', valores: { viajes: 21 } }], [{ clave: 'viajes', nombre: 'Viajes' }], false)
    expect(modelo.unidad).toBe('Cantidad')
    expect(modelo.coordenadas.map(f => f.viajes)).toEqual([0, 21])
  })
  it('no acepta dinero Number ni cantidades inexactas o negativas', () => {
    expect(() => coordenadasGrafico([{ etiqueta: 'A', valores: { valor: 1.1 } }], [{ clave: 'valor', nombre: 'Dinero' }], true)).toThrow()
    expect(() => coordenadasGrafico([{ etiqueta: 'A', valores: { valor: Number.MAX_SAFE_INTEGER + 1 } }], [{ clave: 'valor', nombre: 'Cantidad' }], false)).toThrow()
    expect(() => coordenadasGrafico([{ etiqueta: 'A', valores: { valor: -1 } }], [{ clave: 'valor', nombre: 'Cantidad' }], false)).toThrow()
  })
})
