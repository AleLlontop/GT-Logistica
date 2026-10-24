import { cleanup, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { Link, MemoryRouter } from 'react-router-dom'
import { afterEach, expect, it } from 'vitest'
import { Menu } from './Menu'

const opciones = [
  { codigo: 'viajes', etiqueta: 'Viajes', ruta: '/viajes' },
  { codigo: 'totales', etiqueta: 'Totales', ruta: '/viajes/totales' },
  { codigo: 'clientes', etiqueta: 'Clientes', ruta: '/clientes' },
  { codigo: 'futuro', etiqueta: 'Futuro', ruta: '/futuro' },
]
afterEach(cleanup)
it('abre el módulo activo, pliega otros y permite expandir con teclado', async () => {
  const user = userEvent.setup()
  render(<MemoryRouter initialEntries={['/viajes/totales']}><Menu opciones={opciones} /></MemoryRouter>)
  expect(screen.getByRole('link', { name: 'G&T Logística · Sistema Integral de Gestión' })).toHaveAttribute('href', '/')
  expect(screen.queryByText(/GYT/)).not.toBeInTheDocument()
  const viajes = screen.getByRole('button', { name: 'Viajes' })
  expect(viajes).toHaveAttribute('aria-expanded', 'true')
  expect(screen.getByRole('link', { name: 'Totales' })).toHaveAttribute('aria-current', 'page')
  expect(screen.getByRole('link', { name: 'Viajes' })).not.toHaveAttribute('aria-current')
  const clientes = screen.getByRole('button', { name: 'Clientes' })
  expect(clientes).toHaveAttribute('aria-expanded', 'false')
  expect(screen.queryByRole('link', { name: 'Clientes' })).not.toBeInTheDocument()
  clientes.focus()
  await user.keyboard('{Enter}')
  expect(clientes).toHaveAttribute('aria-expanded', 'true')
  expect(screen.getByRole('link', { name: 'Clientes' })).toBeVisible()
  await user.keyboard(' ')
  expect(clientes).toHaveAttribute('aria-expanded', 'false')
  expect(document.getElementById(clientes.getAttribute('aria-controls')!)).not.toBeVisible()
})
it('una navegación directa abre su grupo y conserva opciones desconocidas', async () => {
  const user = userEvent.setup()
  render(<MemoryRouter initialEntries={['/viajes/42']}><Menu opciones={opciones} /><Link to="/futuro">Ir al módulo nuevo</Link></MemoryRouter>)
  expect(screen.getByRole('link', { name: 'Viajes' })).toHaveAttribute('aria-current', 'page')
  await user.click(screen.getByRole('button', { name: 'Viajes' }))
  expect(screen.queryByRole('link', { name: 'Viajes' })).not.toBeInTheDocument()
  await user.click(screen.getByRole('link', { name: 'Ir al módulo nuevo' }))
  const menu = screen.getByRole('navigation', { name: 'Menú principal' })
  expect(within(menu).getByRole('button', { name: 'Administración' })).toHaveAttribute('aria-expanded', 'true')
  expect(within(menu).getByRole('link', { name: 'Futuro' })).toHaveAttribute('aria-current', 'page')
})
