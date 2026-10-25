import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
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
  const usuario = userEvent.setup()
  render(<App />)
  expect(await screen.findByRole('link', { name: 'Reportes' })).toHaveAttribute('href', '/reportes')
  // Desde el rediseño del menú, cada sección se pliega y sólo abre la de la pantalla actual: el código
  // desconocido sigue dibujándose ([007]), en "Administración", que hay que abrir para verlo.
  await usuario.click(screen.getByRole('button', { name: 'Administración' }))
  expect(screen.getByRole('link', { name: 'Futuro' })).toHaveAttribute('href', '/futuro')
})
