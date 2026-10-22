import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { Persona } from '../../../compartido/tipos'
import { ListadoEmpleados } from './ListadoEmpleados'

const listarEmpleados = vi.fn()
const darDeBajaEmpleado = vi.fn()

vi.mock('../servicios/servicioEmpleados', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioEmpleados')>(
    '../servicios/servicioEmpleados',
  )
  return {
    ...real,
    listarEmpleados: (...args: unknown[]) => listarEmpleados(...args),
    darDeBajaEmpleado: (...args: unknown[]) => darDeBajaEmpleado(...args),
  }
})

function empleado(overrides: Partial<Persona> = {}): Persona {
  return {
    id: 17,
    nombre: 'Ana',
    apellido: 'Pérez',
    dni: '30123456',
    tipo: 'empleado',
    telefono: '221-555-0101',
    email: 'ana.perez@gt.com.ar',
    fechaNacimiento: '1990-05-17',
    activa: true,
    ...overrides,
  }
}

function renderizar(puedeGestionar = false) {
  return render(
    <MemoryRouter>
      <ListadoEmpleados puedeGestionar={puedeGestionar} />
    </MemoryRouter>,
  )
}

describe('ListadoEmpleados', () => {
  beforeEach(() => {
    listarEmpleados.mockReset().mockResolvedValue([empleado()])
    darDeBajaEmpleado.mockReset().mockResolvedValue(undefined)
  })

  it('muestra la ficha y no ofrece escritura a quien sólo consulta', async () => {
    renderizar(false)

    expect(await screen.findByRole('link', { name: 'Pérez, Ana' })).toHaveAttribute(
      'href',
      '/empleados/17',
    )
    expect(screen.queryByRole('link', { name: 'Registrar empleado' })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Editar' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Dar de baja' })).not.toBeInTheDocument()
  })

  it('ofrece alta y edición cuando tiene permiso de gestión', async () => {
    renderizar(true)

    expect(await screen.findByRole('link', { name: 'Registrar empleado' })).toHaveAttribute(
      'href',
      '/empleados/nuevo',
    )
    expect(screen.getByRole('link', { name: 'Editar' })).toHaveAttribute(
      'href',
      '/empleados/17/editar',
    )
  })

  it('distingue lista vacía de una búsqueda sin coincidencias', async () => {
    listarEmpleados.mockResolvedValue([])
    const user = userEvent.setup()
    renderizar()

    expect(await screen.findByText('Todavía no hay empleados registrados.')).toBeInTheDocument()
    await user.type(screen.getByRole('searchbox', { name: 'Nombre, apellido o DNI' }), 'ZZZ')
    expect(await screen.findByText('No hay empleados que coincidan con la búsqueda.')).toBeInTheDocument()
  })

  it('confirma la baja y muestra el conflicto devuelto por el servidor', async () => {
    darDeBajaEmpleado.mockRejectedValue(new Error('Cerrá la caja antes de dar de baja al empleado.'))
    const user = userEvent.setup()
    renderizar(true)

    await user.click(await screen.findByRole('button', { name: 'Dar de baja' }))
    const dialogo = screen.getByRole('dialog')
    expect(within(dialogo).getByText(/¿Confirmás la baja de Pérez, Ana/)).toBeInTheDocument()
    await user.click(within(dialogo).getByRole('button', { name: 'Dar de baja' }))

    await waitFor(() =>
      expect(screen.getByRole('alert')).toHaveTextContent(
        'Cerrá la caja antes de dar de baja al empleado.',
      ),
    )
    expect(darDeBajaEmpleado).toHaveBeenCalledWith(17)
    await waitFor(() => expect(listarEmpleados).toHaveBeenCalledTimes(1))
  })
})
