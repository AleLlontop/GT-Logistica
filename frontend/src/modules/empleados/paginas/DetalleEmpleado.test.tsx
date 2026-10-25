import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { DetalleEmpleado } from './DetalleEmpleado'

const obtenerEmpleado = vi.fn()
const historialDeAdelantos = vi.fn()
const historialDeCajas = vi.fn()

vi.mock('../servicios/servicioEmpleados', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioEmpleados')>(
    '../servicios/servicioEmpleados',
  )
  return {
    ...real,
    obtenerEmpleado: (...args: unknown[]) => obtenerEmpleado(...args),
    historialDeAdelantos: (...args: unknown[]) => historialDeAdelantos(...args),
    historialDeCajas: (...args: unknown[]) => historialDeCajas(...args),
  }
})

function renderizar(puedeGestionar = false) {
  return render(
    <MemoryRouter initialEntries={['/empleados/17']}>
      <Routes>
        <Route path="/empleados/:id" element={<DetalleEmpleado puedeGestionar={puedeGestionar} />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('DetalleEmpleado', () => {
  beforeEach(() => {
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
    historialDeAdelantos.mockReset().mockResolvedValue([
      { id: 81, fecha: '2026-09-10', persona: { id: 17, apellido: 'Pérez', nombre: 'Ana', dni: '30123456' }, tipo: 'empleado', motivo: 'Viáticos', importe: 15000, estado: 'aprobado' },
    ])
    historialDeCajas.mockReset().mockResolvedValue([
      { id: 22, responsable: { id: 41, nombre: 'ana.perez' }, fechaApertura: '2026-09-10T11:00:00Z', estado: 'cerrada', fechaCierre: '2026-09-10T19:00:00Z', saldoFinal: 15000 },
    ])
  })

  it('muestra los datos personales y ambos historiales', async () => {
    renderizar()

    expect(await screen.findByRole('heading', { name: 'Pérez, Ana' })).toBeInTheDocument()
    expect(screen.getByText('30123456')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Historial de adelantos' })).toBeInTheDocument()
    expect(screen.getByText('Viáticos')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Cajas a cargo' })).toBeInTheDocument()
    expect(screen.getByText('ana.perez')).toBeInTheDocument()
  })

  it('muestra el estado del adelanto con la palabra del módulo, no con el código', async () => {
    renderizar()

    expect(await screen.findByText('Aprobado')).toBeInTheDocument()
    expect(screen.queryByText('aprobado')).not.toBeInTheDocument()
  })

  it('un 403 dice que falta permiso y a quién pedírselo', async () => {
    historialDeCajas.mockRejectedValue(
      new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso.' }),
    )
    renderizar()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'No tenés permiso para ver este empleado o alguno de sus historiales. Si lo necesitás, pedíselo a quien administra los usuarios del sistema.',
    )
  })

  it('oculta la acción de modificar para quien sólo consulta', async () => {
    renderizar(false)

    await screen.findByRole('heading', { name: 'Pérez, Ana' })
    expect(screen.queryByRole('link', { name: 'Modificar empleado' })).not.toBeInTheDocument()
  })

  it('muestra la acción de modificar para quien gestiona', async () => {
    renderizar(true)

    expect(await screen.findByRole('link', { name: 'Modificar empleado' })).toHaveAttribute(
      'href',
      '/empleados/17/editar',
    )
  })

  it('no presenta un error de historial como si fuera una lista vacía', async () => {
    historialDeCajas.mockRejectedValue(new Error('Sin permiso para caja'))
    renderizar()

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'No pudimos cargar la ficha y sus historiales. Volvé a intentar en unos minutos.',
    )
    expect(screen.queryByText('No hay cajas asociadas al usuario de este empleado.')).not.toBeInTheDocument()
  })
})
