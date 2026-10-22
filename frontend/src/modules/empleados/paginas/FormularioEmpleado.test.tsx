import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { FormularioEmpleado } from './FormularioEmpleado'

const crearEmpleado = vi.fn()
const modificarEmpleado = vi.fn()
const obtenerEmpleado = vi.fn()

vi.mock('../servicios/servicioEmpleados', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioEmpleados')>(
    '../servicios/servicioEmpleados',
  )
  return {
    ...real,
    crearEmpleado: (...args: unknown[]) => crearEmpleado(...args),
    modificarEmpleado: (...args: unknown[]) => modificarEmpleado(...args),
    obtenerEmpleado: (...args: unknown[]) => obtenerEmpleado(...args),
  }
})

function UbicacionActual() {
  const ubicacion = useLocation()
  return <output data-testid="ubicacion">{ubicacion.pathname}</output>
}

function renderizar(ruta = '/empleados/nuevo', puedeGestionar = true) {
  return render(
    <MemoryRouter initialEntries={[ruta]}>
      <UbicacionActual />
      <Routes>
        <Route path="/empleados/nuevo" element={<FormularioEmpleado puedeGestionar={puedeGestionar} />} />
        <Route path="/empleados/:id/editar" element={<FormularioEmpleado puedeGestionar={puedeGestionar} />} />
        <Route path="/empleados" element={<h1>Listado de empleados</h1>} />
      </Routes>
    </MemoryRouter>,
  )
}

async function completarFormulario() {
  const user = userEvent.setup()
  await user.type(screen.getByLabelText('Nombre'), 'Ana')
  await user.type(screen.getByLabelText('Apellido'), 'Pérez')
  await user.type(screen.getByLabelText('DNI'), '30123456')
  await user.type(screen.getByLabelText('Fecha de nacimiento'), '1990-05-17')
  await user.type(screen.getByLabelText('Teléfono'), '221-555-0101')
  await user.type(screen.getByLabelText('Email'), 'ana.perez@gt.com.ar')
  await user.click(screen.getByRole('button', { name: 'Guardar empleado' }))
}

describe('FormularioEmpleado', () => {
  beforeEach(() => {
    crearEmpleado.mockReset().mockResolvedValue({ id: 17 })
    modificarEmpleado.mockReset().mockResolvedValue({ id: 17 })
    obtenerEmpleado.mockReset().mockResolvedValue({
      id: 17,
      nombre: 'Ana',
      apellido: 'Pérez',
      dni: '30123456',
      tipo: 'empleado',
      telefono: '221-555-0101',
      email: 'ana.perez@gt.com.ar',
      fechaNacimiento: '1990-05-17',
      activa: true,
    })
  })

  it('valida datos obligatorios sin llamar al servicio', async () => {
    const user = userEvent.setup()
    renderizar()
    await user.click(screen.getByRole('button', { name: 'Guardar empleado' }))

    expect(await screen.findByText('Revisá los campos marcados antes de guardar.')).toBeInTheDocument()
    expect(crearEmpleado).not.toHaveBeenCalled()
  })

  it('crea empleado y navega al listado', async () => {
    renderizar()
    await completarFormulario()

    expect(crearEmpleado).toHaveBeenCalledWith({
      nombre: 'Ana',
      apellido: 'Pérez',
      dni: '30123456',
      fechaNacimiento: '1990-05-17',
      telefono: '221-555-0101',
      email: 'ana.perez@gt.com.ar',
    })
    expect(await screen.findByText('Listado de empleados')).toBeInTheDocument()
  })

  it('no permite cargar ni guardar si no tiene permiso de gestión', async () => {
    renderizar('/empleados/nuevo', false)

    expect(screen.getByRole('alert')).toHaveTextContent('No tenés permiso para registrar ni modificar empleados.')
    expect(screen.queryByLabelText('DNI')).not.toBeInTheDocument()
    expect(crearEmpleado).not.toHaveBeenCalled()
  })

  it('carga los datos para modificar un empleado', async () => {
    renderizar('/empleados/17/editar')

    expect(await screen.findByLabelText('Nombre')).toHaveValue('Ana')
    expect(screen.getByLabelText('DNI')).toHaveValue('30123456')
    expect(obtenerEmpleado).toHaveBeenCalledWith(17)
  })
})
