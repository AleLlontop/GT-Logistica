import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import type { Persona } from '../../../compartido/tipos'
import { ListadoEmpleados } from './ListadoEmpleados'

const listarEmpleados = vi.fn()
const darDeBajaEmpleado = vi.fn()
const reactivarEmpleado = vi.fn()

vi.mock('../servicios/servicioEmpleados', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioEmpleados')>(
    '../servicios/servicioEmpleados',
  )
  return {
    ...real,
    listarEmpleados: (...args: unknown[]) => listarEmpleados(...args),
    darDeBajaEmpleado: (...args: unknown[]) => darDeBajaEmpleado(...args),
    reactivarEmpleado: (...args: unknown[]) => reactivarEmpleado(...args),
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
    reactivarEmpleado.mockReset().mockResolvedValue(undefined)
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

  it('un 403 dice que falta permiso y a quién pedírselo', async () => {
    listarEmpleados.mockRejectedValue(
      new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso.' }),
    )
    renderizar()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'No tenés permiso para ver los empleados. Si lo necesitás, pedíselo a quien administra los usuarios del sistema.',
    )
  })

  it('una respuesta vieja que llega tarde no pisa a la de la búsqueda actual', async () => {
    // La primera consulta (sin texto) queda colgada hasta después de la segunda.
    let resolverPrimera: (lista: Persona[]) => void = () => {}
    listarEmpleados
      .mockReset()
      .mockReturnValueOnce(new Promise<Persona[]>((resolver) => (resolverPrimera = resolver)))
      .mockResolvedValue([empleado({ id: 18, nombre: 'Bruno', apellido: 'Gómez' })])

    const user = userEvent.setup()
    renderizar()

    await user.type(screen.getByRole('searchbox', { name: 'Nombre, apellido o DNI' }), 'G')
    expect(await screen.findByRole('link', { name: 'Gómez, Bruno' })).toBeInTheDocument()

    resolverPrimera([empleado()])

    await waitFor(() => expect(screen.getByRole('link', { name: 'Gómez, Bruno' })).toBeInTheDocument())
    expect(screen.queryByRole('link', { name: 'Pérez, Ana' })).not.toBeInTheDocument()
  })

  it('ofrece Reactivar en lugar de Dar de baja a un empleado dado de baja', async () => {
    listarEmpleados.mockResolvedValue([empleado({ activa: false })])
    renderizar(true)

    expect(await screen.findByRole('button', { name: 'Reactivar' })).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Dar de baja' })).not.toBeInTheDocument()
  })

  it('no ofrece Reactivar a quien sólo consulta', async () => {
    listarEmpleados.mockResolvedValue([empleado({ activa: false })])
    renderizar(false)

    expect(await screen.findByText('Dado de baja')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Reactivar' })).not.toBeInTheDocument()
  })

  it('reactiva con confirmación, recarga y lo anuncia', async () => {
    listarEmpleados.mockResolvedValue([empleado({ activa: false })])
    const user = userEvent.setup()
    renderizar(true)

    await user.click(await screen.findByRole('button', { name: 'Reactivar' }))
    const dialogo = screen.getByRole('dialog')
    expect(within(dialogo).getByText('¿Confirmás la reactivación de Pérez, Ana?')).toBeInTheDocument()

    listarEmpleados.mockResolvedValue([empleado()])
    await user.click(within(dialogo).getByRole('button', { name: 'Reactivar' }))

    await waitFor(() =>
      expect(screen.getByRole('status')).toHaveTextContent('Pérez, Ana volvió a estar activo.'),
    )
    expect(reactivarEmpleado).toHaveBeenCalledWith(17)
    expect(await screen.findByRole('button', { name: 'Dar de baja' })).toBeInTheDocument()
  })

  it('cancelar la reactivación no cambia nada', async () => {
    listarEmpleados.mockResolvedValue([empleado({ activa: false })])
    const user = userEvent.setup()
    renderizar(true)

    await user.click(await screen.findByRole('button', { name: 'Reactivar' }))
    await user.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Cancelar' }))

    expect(reactivarEmpleado).not.toHaveBeenCalled()
  })
})
