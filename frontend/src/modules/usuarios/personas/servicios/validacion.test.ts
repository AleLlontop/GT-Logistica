import { describe, expect, it } from 'vitest'
import { errorDeNombre, errorDeTelefono } from './validacion'

// FR-026a. Los mismos casos están en `ValidadorPersonaTests.cs`: si uno cambia, el otro también.

describe('errorDeNombre', () => {
  it.each(['Juan', 'María José', 'Ñuñez', "O'Connor", 'D’Angelo', 'Pérez-Gómez', 'J.', 'Zoë'])(
    'acepta %s',
    (nombre) => {
      expect(errorDeNombre(nombre, 'nombre')).toBeUndefined()
    },
  )

  it.each(['Juan3', 'Juan 2do', 'Juan_Pérez', 'Juan@', '...', "- '"])('rechaza %s', (nombre) => {
    expect(errorDeNombre(nombre, 'nombre')).toBe(
      'El nombre sólo puede tener letras, espacios, guiones, apóstrofes y puntos.',
    )
  })

  it('nombra el campo en el mensaje', () => {
    expect(errorDeNombre('Gómez2', 'apellido')).toBe(
      'El apellido sólo puede tener letras, espacios, guiones, apóstrofes y puntos.',
    )
  })

  it('admite 100 caracteres y rechaza 101, sin contar los espacios de los bordes', () => {
    expect(errorDeNombre(`  ${'a'.repeat(100)}  `, 'nombre')).toBeUndefined()
    expect(errorDeNombre('a'.repeat(101), 'nombre')).toBe('Puede tener hasta 100 caracteres.')
  })
})

describe('errorDeTelefono', () => {
  it.each(['221-555-0101', '+54 9 (221) 555.0101', '455512', '12345678901234567890'])(
    'acepta %s',
    (telefono) => {
      expect(errorDeTelefono(telefono)).toBeUndefined()
    },
  )

  it.each([
    '45551',
    '123456789012345678901',
    '221-ABC-0101',
    '4555-12/13',
    '11-5555-5555 int. 23',
    `${'1'.repeat(20)}${'-'.repeat(11)}`,
  ])('rechaza %s', (telefono) => {
    expect(errorDeTelefono(telefono)).toBe(
      'El teléfono tiene que tener entre 6 y 20 números, y sólo puede sumar +, espacios, guiones, paréntesis y puntos.',
    )
  })
})
