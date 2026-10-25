/**
 * Formato de nombre, apellido y teléfono de una persona (FR-026a).
 *
 * **Es copia de `ValidadorPersona` del backend** y tiene que decir lo mismo: si la pantalla acepta
 * algo que el servidor rechaza, el usuario ve un "Revisá los campos marcados en rojo." sin saber
 * qué está mal. Lo usan el formulario de Personas y el de Empleados, que guardan la misma persona.
 * Los casos borde están repetidos en `validacion.test.ts` y en `ValidadorPersonaTests.cs`.
 *
 * Las dos funciones reciben un valor ya no vacío: el mensaje de campo vacío lo pone cada formulario.
 */

export const LARGO_MAXIMO_NOMBRE = 100
export const LARGO_MAXIMO_TELEFONO = 30

const CARACTERES_DE_NOMBRE = /^[\p{L} '’.-]+$/u
const UNA_LETRA = /\p{L}/u
const CARACTERES_DE_TELEFONO = /^[0-9+ ()\-.]+$/

/** El mensaje del error, o `undefined` si el nombre o el apellido es válido. */
export function errorDeNombre(valor: string, campo: 'nombre' | 'apellido'): string | undefined {
  const nombre = valor.trim()

  if (nombre.length > LARGO_MAXIMO_NOMBRE) {
    return `Puede tener hasta ${LARGO_MAXIMO_NOMBRE} caracteres.`
  }

  if (!CARACTERES_DE_NOMBRE.test(nombre) || !UNA_LETRA.test(nombre)) {
    return `El ${campo} sólo puede tener letras, espacios, guiones, apóstrofes y puntos.`
  }

  return undefined
}

/** El mensaje del error, o `undefined` si el teléfono es válido. */
export function errorDeTelefono(valor: string): string | undefined {
  const telefono = valor.trim()
  const digitos = telefono.replace(/[^0-9]/g, '').length

  const valido =
    telefono.length <= LARGO_MAXIMO_TELEFONO &&
    CARACTERES_DE_TELEFONO.test(telefono) &&
    digitos >= 6 &&
    digitos <= 20

  return valido
    ? undefined
    : 'El teléfono tiene que tener entre 6 y 20 números, y sólo puede sumar +, espacios, guiones, paréntesis y puntos.'
}
