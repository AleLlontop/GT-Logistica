import { peticion } from '../../../../compartido/clienteHttp'
import type { Persona } from '../../../../compartido/tipos'

/**
 * Padrón de personas (choferes y empleados).
 *
 * Por ahora sólo lectura: el alta, la edición y la baja llegan con la User Story 6. La lectura va
 * primero porque el selector de persona del formulario de usuario la necesita.
 */

interface FiltrosPersonas {
  /** Fragmento a buscar en nombre, apellido o DNI. Coincidencia parcial, sin distinguir mayúsculas. */
  texto?: string
  /** `true` deja afuera a las dadas de baja: es lo que usa el selector del formulario (FR-023). */
  soloActivas?: boolean
}

export function listarPersonas({ texto, soloActivas }: FiltrosPersonas = {}): Promise<Persona[]> {
  const parametros = new URLSearchParams()

  if (texto) {
    parametros.set('texto', texto)
  }

  if (soloActivas) {
    parametros.set('soloActivas', 'true')
  }

  const consulta = parametros.toString()

  return peticion<Persona[]>(`/personas${consulta ? `?${consulta}` : ''}`)
}

export function obtenerPersona(id: number): Promise<Persona> {
  return peticion<Persona>(`/personas/${id}`)
}

/** Los siete datos de FR-026, ni uno más. */
export interface DatosPersona {
  nombre: string
  apellido: string
  dni: string
  tipo: 'chofer' | 'empleado'
  telefono: string
  email: string
  /** Formato `AAAA-MM-DD`, que es lo que entrega un `input type="date"`. */
  fechaNacimiento: string
}

export function crearPersona(datos: DatosPersona): Promise<Persona> {
  return peticion<Persona>('/personas', { metodo: 'POST', cuerpo: datos })
}

export function modificarPersona(id: number, datos: DatosPersona): Promise<Persona> {
  return peticion<Persona>(`/personas/${id}`, { metodo: 'PUT', cuerpo: datos })
}

/** Baja lógica: la persona queda inactiva, el registro no se borra (FR-022). */
export function darDeBajaPersona(id: number): Promise<void> {
  return peticion<void>(`/personas/${id}`, { metodo: 'DELETE' })
}

export const TIPOS_DE_PERSONA: ReadonlyArray<{ codigo: DatosPersona['tipo']; nombre: string }> = [
  { codigo: 'chofer', nombre: 'Chofer' },
  { codigo: 'empleado', nombre: 'Empleado' },
]

/** Nombre para mostrar, en el orden habitual en Argentina. */
export function nombreCompleto(persona: Persona): string {
  return `${persona.apellido}, ${persona.nombre}`
}
