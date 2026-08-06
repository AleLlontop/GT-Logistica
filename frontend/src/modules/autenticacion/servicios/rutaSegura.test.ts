import { describe, expect, it } from 'vitest'
import { rutaInternaSegura } from './rutaSegura'

describe('rutaInternaSegura (FR-026)', () => {
  it('conserva una ruta interna de la aplicación', () => {
    expect(rutaInternaSegura('/usuarios')).toBe('/usuarios')
    expect(rutaInternaSegura('/usuarios?estado=activo')).toBe('/usuarios?estado=activo')
  })

  it('cae a la pantalla de inicio cuando no hay destino guardado', () => {
    expect(rutaInternaSegura(null)).toBe('/')
    expect(rutaInternaSegura(undefined)).toBe('/')
    expect(rutaInternaSegura('')).toBe('/')
  })

  it.each([
    ['https://ejemplo.com', 'URL absoluta a otro sitio'],
    ['//ejemplo.com', 'URL relativa al protocolo'],
    ['/\\ejemplo.com', 'barra invertida que el navegador normaliza'],
    ['\\\\ejemplo.com', 'ruta UNC'],
    ['javascript:alert(1)', 'esquema javascript'],
  ])('descarta %s (%s)', (destino) => {
    expect(rutaInternaSegura(destino)).toBe('/')
  })

  it('descarta la propia pantalla de ingreso para no dejar al usuario dando vueltas', () => {
    expect(rutaInternaSegura('/ingresar')).toBe('/')
    expect(rutaInternaSegura('/ingresar?volver=1')).toBe('/')
  })
})
