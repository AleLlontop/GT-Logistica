import { cleanup, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { Layout } from './Layout'

const opciones = [
  { codigo: 'clientes', etiqueta: 'Clientes', ruta: '/clientes' },
  { codigo: 'modulo-futuro', etiqueta: 'Opción nueva', ruta: '/futuro' },
]
afterEach(cleanup)

describe('Shell G&T Logística', () => {
  it('conserva opciones recibidas, marca el destino actual y permite cerrar sesión', async () => {
    const salir = vi.fn()
    render(<MemoryRouter initialEntries={['/clientes']}><Layout username="ana" opcionesMenu={opciones} onCerrarSesion={salir}><h1>Listado de clientes</h1></Layout></MemoryRouter>)
    const menu = screen.getByRole('navigation', { name: 'Menú principal' })
    expect(within(menu).getByRole('link', { name: 'Clientes' })).toHaveAttribute('aria-current', 'page')
    await userEvent.setup().click(within(menu).getByRole('button', { name: 'Administración' }))
    expect(within(menu).getByRole('link', { name: 'Opción nueva' })).toHaveAttribute('href', '/futuro')
    expect(screen.queryByRole('link', { name: 'Viajes' })).not.toBeInTheDocument()
    expect(screen.getByRole('main')).toHaveAttribute('id', 'contenido-principal')
    await userEvent.setup().click(screen.getByRole('button', { name: 'Cerrar sesión' }))
    expect(salir).toHaveBeenCalledOnce()
  })
  it('el menú móvil retiene foco, cierra con Escape y devuelve el foco al disparador', async () => {
    render(<MemoryRouter><Layout username="ana" opcionesMenu={opciones} onCerrarSesion={() => {}}>Contenido</Layout></MemoryRouter>)
    const user = userEvent.setup()
    const trigger = screen.getByRole('button', { name: 'Abrir menú' })
    await user.click(trigger)
    const drawer = screen.getByRole('dialog', { name: 'Navegación' })
    expect(within(drawer).getByRole('navigation', { name: 'Menú principal' })).toBeVisible()
    await user.keyboard('{Escape}')
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(trigger).toHaveFocus()
  })
  it('sin permisos conserva cuenta y salida, sin inventar navegación', () => {
    render(<MemoryRouter><Layout username="ana" opcionesMenu={[]} onCerrarSesion={() => {}}>Contenido</Layout></MemoryRouter>)
    const menu = screen.getByRole('navigation', { name: 'Menú principal' })
    expect(within(menu).getByRole('link', { name: 'Inicio' })).toHaveAttribute('href', '/')
    expect(within(menu).queryByRole('button')).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Cambiar contraseña' })).toBeVisible()
    expect(screen.getByRole('button', { name: 'Cerrar sesión' })).toBeVisible()
  })
})
