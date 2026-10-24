import { cleanup, render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, expect, it } from 'vitest'
import { PantallaInicio } from './PantallaInicio'
import { accesosPrioritarios } from '../../../compartido/seccionesDeMenu'
import type { Sesion } from '../servicios/sesion'

const opcion = (codigo: string) => ({ codigo, etiqueta: codigo, ruta: '/' + codigo })
const sesion: Sesion = { username: 'ana', roles: [{ codigo: 'trafico', nombre: 'Tráfico' }], permisos: [], opcionesMenu: [] }
afterEach(cleanup)
it('limita la portada a ocho accesos autorizados y prioriza las consultas principales', () => {
  const opciones = ['usuarios', 'personas', 'empleados', 'viajes', 'facturas', 'consultar-liquidacion', 'consultar-caja', 'consultar-ordenes-proveedor', 'reportes', 'flota', 'proveedores'].map(opcion)
  render(<MemoryRouter><PantallaInicio sesion={{ ...sesion, opcionesMenu: opciones }} /></MemoryRouter>)
  expect(screen.getByRole('heading', { name: 'Hola, ana' })).toBeVisible()
  expect(screen.getByText('G&T Logística · Centro de operaciones')).toBeVisible()
  const links = within(screen.getByRole('list')).getAllByRole('link')
  expect(links).toHaveLength(8)
  expect(links[0]).toHaveAttribute('href', '/viajes')
  expect(screen.queryByRole('link', { name: /usuarios/ })).not.toBeInTheDocument()
  expect(accesosPrioritarios(opciones).every(o => opciones.includes(o))).toBe(true)
})
it('un rol con una única opción secundaria conserva un acceso útil', () => {
  expect(accesosPrioritarios([opcion('tipos-documentacion')])).toEqual([opcion('tipos-documentacion')])
})
it('sin opciones mantiene la bienvenida y no inventa accesos ni métricas', () => {
  render(<MemoryRouter><PantallaInicio sesion={sesion} /></MemoryRouter>)
  expect(screen.getByText(/Por ahora no tenés funcionalidades/)).toBeVisible()
  expect(screen.queryByRole('link')).not.toBeInTheDocument()
})
