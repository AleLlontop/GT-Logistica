import { render, screen } from '@testing-library/react'
import { afterEach, expect, it, vi } from 'vitest'
import App from '../../../App'
import { obtenerSesion } from '../../autenticacion/servicios/sesion'
import { consultarAnalisis, consultarDashboard } from '../servicios/servicioAnalisis'
vi.mock('../../autenticacion/servicios/sesion', async original => ({ ...await original<typeof import('../../autenticacion/servicios/sesion')>(), obtenerSesion: vi.fn() }))
vi.mock('../servicios/servicioAnalisis', async original => ({ ...await original<typeof import('../servicios/servicioAnalisis')>(), consultarAnalisis: vi.fn(), consultarDashboard: vi.fn() }))
afterEach(() => vi.clearAllMocks())
it.each(['/reportes', '/reportes/viajes', '/reportes/facturacion', '/reportes/liquidaciones', '/reportes/caja', '/reportes/flota', '/reportes/vencimientos'])('ruta directa %s respeta falta de permiso sin consulta', async ruta => {
  window.history.replaceState(null, '', ruta)
  vi.mocked(obtenerSesion).mockResolvedValue({ username: 'lector', roles: [], opcionesMenu: [], permisos: [] })
  render(<App />)
  expect(await screen.findByText(/No tenés permiso para consultar este reporte/)).toBeInTheDocument()
  expect(consultarAnalisis).not.toHaveBeenCalled(); expect(consultarDashboard).not.toHaveBeenCalled()
})
it('menú recibido conserva entrada Reportes y códigos desconocidos', async () => {
  window.history.replaceState(null, '', '/reportes')
  vi.mocked(obtenerSesion).mockResolvedValue({ username: 'lector', roles: [], permisos: [], opcionesMenu: [{ codigo: 'reportes', etiqueta: 'Reportes', ruta: '/reportes' }, { codigo: 'futuro', etiqueta: 'Futuro', ruta: '/futuro' }] })
  render(<App />)
  expect(await screen.findByRole('link', { name: 'Reportes' })).toHaveAttribute('href', '/reportes')
  expect(screen.getByRole('link', { name: 'Futuro' })).toBeInTheDocument()
})
