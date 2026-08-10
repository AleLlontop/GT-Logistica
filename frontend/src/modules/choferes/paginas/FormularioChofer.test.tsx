import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { FormularioChofer } from './FormularioChofer'
import type { Transportista } from '../transportistas/servicioTransportistas'
import type { ChoferDetalle } from '../servicios/servicioChoferes'

const listarTransportistas = vi.fn()
const crearChofer = vi.fn()

vi.mock('../transportistas/servicioTransportistas', async () => {
  const real = await vi.importActual<
    typeof import('../transportistas/servicioTransportistas')
  >('../transportistas/servicioTransportistas')
  return {
    ...real,
    listarTransportistas: (...args: unknown[]) => listarTransportistas(...args),
  }
})

vi.mock('../servicios/servicioChoferes', async () => {
  const real = await vi.importActual<
    typeof import('../servicios/servicioChoferes')
  >('../servicios/servicioChoferes')
  return {
    ...real,
    crearChofer: (...args: unknown[]) => crearChofer(...args),
  }
})

const gtlogistica: Transportista = {
  id: 1,
  nombre: 'G&T Logística S.A.',
  cuit: '30710000006',
  tipo: 'juridica',
  telefono: '11-5555-5555',
  email: 'info@gt.com.ar',
  activo: true,
  choferesActivos: 0,
  vehiculosActivos: 0,
}

function choferCreado(reutilizoPersona: boolean): ChoferDetalle {
  return {
    id: 7,
    nombre: 'Ramona',
    apellido: 'Gómez',
    dni: '31111222',
    cuil: '27311112223',
    fechaNacimiento: '1990-05-17',
    telefono: '11-5555-5555',
    email: 'ramona@gt.com.ar',
    transportista: { id: 1, nombre: 'G&T Logística S.A.' },
    activo: true,
    estadoDocumentacion: 'sinDocumentacion',
    personaId: 12,
    documentos: [],
    reutilizoPersona,
  }
}

function renderizar() {
  return render(
    <MemoryRouter>
      <FormularioChofer />
    </MemoryRouter>,
  )
}

async function completarYGuardar() {
  const usuario = userEvent.setup()

  await screen.findByLabelText('DNI')

  await usuario.type(screen.getByLabelText('DNI'), '31111222')
  await usuario.type(screen.getByLabelText('Nombre'), 'Ramona')
  await usuario.type(screen.getByLabelText('Apellido'), 'Gómez')
  await usuario.type(screen.getByLabelText('Fecha de nacimiento'), '1990-05-17')
  await usuario.type(screen.getByLabelText('CUIL'), '27311112223')
  await usuario.type(screen.getByLabelText('Teléfono'), '11-5555-5555')
  await usuario.type(screen.getByLabelText('Email'), 'ramona@gt.com.ar')

  await usuario.click(screen.getByRole('button', { name: 'Guardar chofer' }))
}

describe('FormularioChofer', () => {
  beforeEach(() => {
    listarTransportistas.mockReset()
    listarTransportistas.mockResolvedValue([gtlogistica])
    crearChofer.mockReset()
    crearChofer.mockResolvedValue(choferCreado(false))
  })

  it('bloquea el alta cuando no hay transportistas activos, con enlace a esa pantalla (US2 esc. 4)', async () => {
    listarTransportistas.mockResolvedValue([])

    renderizar()

    expect(
      await screen.findByText(
        'No hay transportistas activos. Registrá uno desde la pantalla Transportistas.',
      ),
    ).toBeInTheDocument()

    // No hay formulario que completar, y sí hay a dónde ir a resolverlo.
    expect(screen.queryByLabelText('DNI')).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Registrar un transportista' })).toHaveAttribute(
      'href',
      '/transportistas/nuevo',
    )
  })

  it('sólo ofrece transportistas activos en el selector (FR-008)', async () => {
    renderizar()

    await screen.findByLabelText('Transportista')

    expect(listarTransportistas).toHaveBeenCalledWith('', true)
  })

  it('confirma el alta sin mencionar reutilización cuando la persona es nueva', async () => {
    renderizar()
    await completarYGuardar()

    expect(
      await screen.findByText('El chofer Gómez, Ramona se registró correctamente.'),
    ).toBeInTheDocument()
  })

  it('avisa que se reutilizó una persona del padrón cuando el DNI ya estaba (FR-006)', async () => {
    crearChofer.mockResolvedValue(choferCreado(true))

    renderizar()
    await completarYGuardar()

    expect(
      await screen.findByText(
        'El chofer Gómez, Ramona se registró correctamente, reutilizando la persona que ya estaba en el padrón.',
      ),
    ).toBeInTheDocument()
  })
})
