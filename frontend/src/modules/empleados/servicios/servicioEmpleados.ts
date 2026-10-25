import { peticion } from '../../../compartido/clienteHttp'
import type { Persona } from '../../../compartido/tipos'
import type { AdelantoListado } from '../../adelantos/servicios/servicioAdelantos'
import type { CajaListado } from '../../caja/servicios/servicioCaja'

export interface DatosEmpleado {
  nombre: string
  apellido: string
  dni: string
  telefono: string
  email: string
  /** Formato AAAA-MM-DD, requerido por Persona en el backend vigente. */
  fechaNacimiento: string
}

export interface FiltrosEmpleados {
  texto?: string
  soloActivas?: boolean
}

function query(filtros: FiltrosEmpleados): string {
  const parametros = new URLSearchParams()
  if (filtros.texto) parametros.set('texto', filtros.texto)
  if (filtros.soloActivas) parametros.set('soloActivas', 'true')
  const valor = parametros.toString()
  return valor ? `?${valor}` : ''
}

export function listarEmpleados(filtros: FiltrosEmpleados = {}): Promise<Persona[]> {
  return peticion<Persona[]>(`/empleados${query(filtros)}`)
}

export function obtenerEmpleado(id: number): Promise<Persona> {
  return peticion<Persona>(`/empleados/${id}`)
}

export function crearEmpleado(datos: DatosEmpleado): Promise<Persona> {
  return peticion<Persona>('/empleados', {
    metodo: 'POST',
    cuerpo: { ...datos, tipo: 'empleado' },
  })
}

export function modificarEmpleado(id: number, datos: DatosEmpleado): Promise<Persona> {
  return peticion<Persona>(`/empleados/${id}`, {
    metodo: 'PUT',
    cuerpo: { ...datos, tipo: 'empleado' },
  })
}

export function darDeBajaEmpleado(id: number): Promise<void> {
  return peticion<void>(`/empleados/${id}`, { metodo: 'DELETE' })
}

/** Vuelve a dejar activo a un empleado dado de baja, con los datos que tenía. */
export function reactivarEmpleado(id: number): Promise<void> {
  return peticion<void>(`/empleados/${id}/reactivacion`, { metodo: 'POST' })
}

export function historialDeAdelantos(id: number): Promise<AdelantoListado[]> {
  return peticion<AdelantoListado[]>(`/empleados/${id}/adelantos`)
}

export function historialDeCajas(id: number): Promise<CajaListado[]> {
  return peticion<CajaListado[]>(`/empleados/${id}/cajas`)
}