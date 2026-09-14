import { describe, expect, it } from 'vitest'
import { formatearCuit } from './cuit'

describe('formatearCuit', () => {
  it('separa con guiones un CUIT de once dígitos', () => {
    expect(formatearCuit('20123456786')).toBe('20-12345678-6')
  })

  /** El caso borde de las dos copias que reemplaza, conservado tal cual (research §11b). */
  it('deja sin tocar cualquier otro largo', () => {
    expect(formatearCuit('2012345678')).toBe('2012345678')
    expect(formatearCuit('')).toBe('')
  })
})
