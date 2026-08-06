import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { CodigosError } from '../servicios/api'
import { FormularioTransportista } from './FormularioTransportista'

const crearTransportista = vi.fn()

vi.mock('./servicioTransportistas', async () => {
  const real = await vi.importActual<typeof import('./servicioTransportistas')>('./servicioTransportistas')
  return {
    ...real,
    crearTransportista: (...args: unknown[]) => crearTransportista(...args),
  }
})

function renderizar() {
  return render(
    <MemoryRouter>
      <FormularioTransportista />
    </MemoryRouter>,
  )
}

describe('FormularioTransportista', () => {
  beforeEach(() => {
    crearTransportista.mockReset()
    crearTransportista.mockResolvedValue({ id: 1, choferesActivos: 0 })
  })

  it('muestra un error si el CUIT ya está registrado', async () => {
    crearTransportista.mockRejectedValue(
      new ErrorHttp(400, {
        codigo: CodigosError.cuitDuplicado,
        mensaje: 'Ese CUIT ya está registrado para otro transportista.',
        campo: 'cuit',
      }),
    )

    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(screen.getByLabelText(/Razón social/i), 'G&T')
    await usuario.type(screen.getByLabelText(/CUIT/i), '30710000006')
    await usuario.selectOptions(screen.getByLabelText(/Tipo de persona/i), 'juridica')
    await usuario.type(screen.getByLabelText(/Teléfono/i), '11')
    await usuario.type(screen.getByLabelText(/Email/i), 'info@gt.com')

    await usuario.click(screen.getByRole('button', { name: 'Guardar transportista' }))

    expect(await screen.findByText('Ese CUIT ya está registrado para otro transportista.')).toBeInTheDocument()
    expect(screen.getByText('Ya registrado.')).toBeInTheDocument()
  })
})
