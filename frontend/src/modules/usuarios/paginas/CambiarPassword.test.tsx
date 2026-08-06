import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { CambiarPassword } from './CambiarPassword'

const peticion = vi.fn()

vi.mock('../../../compartido/clienteHttp', async () => {
  const real = await vi.importActual<typeof import('../../../compartido/clienteHttp')>(
    '../../../compartido/clienteHttp',
  )

  return { ...real, peticion: (...args: unknown[]) => peticion(...args) }
})

async function completar(
  usuario: ReturnType<typeof userEvent.setup>,
  actual: string,
  nueva: string,
  repeticion: string,
) {
  if (actual !== '') {
    await usuario.type(screen.getByLabelText('Contraseña actual'), actual)
  }

  if (nueva !== '') {
    await usuario.type(screen.getByLabelText('Contraseña nueva'), nueva)
  }

  if (repeticion !== '') {
    await usuario.type(screen.getByLabelText('Repetir contraseña nueva'), repeticion)
  }

  await usuario.click(screen.getByRole('button', { name: 'Cambiar contraseña' }))
}

describe('CambiarPassword', () => {
  beforeEach(() => {
    peticion.mockReset()
    peticion.mockResolvedValue(undefined)
  })

  it('abre con los tres campos vacíos y enmascarados (FR-030)', () => {
    render(<CambiarPassword />)

    for (const etiqueta of [
      'Contraseña actual',
      'Contraseña nueva',
      'Repetir contraseña nueva',
    ]) {
      const campo = screen.getByLabelText(etiqueta)

      expect(campo).toHaveValue('')
      expect(campo).toHaveAttribute('type', 'password')
    }
  })

  it('rechaza cuando las dos contraseñas nuevas no coinciden, sin llamar al servidor', async () => {
    const usuario = userEvent.setup()
    render(<CambiarPassword />)

    await completar(usuario, 'LaActual.123', 'LaNueva.4567', 'OtraDistinta.89')

    expect(await screen.findByText('Las dos contraseñas nuevas no coinciden.')).toBeInTheDocument()
    expect(peticion).not.toHaveBeenCalled()
  })

  it('rechaza una contraseña nueva de menos de 8 caracteres, sin llamar al servidor', async () => {
    const usuario = userEvent.setup()
    render(<CambiarPassword />)

    await completar(usuario, 'LaActual.123', '1234567', '1234567')

    expect(
      await screen.findByText('La contraseña nueva tiene que tener al menos 8 caracteres.'),
    ).toBeInTheDocument()

    expect(peticion).not.toHaveBeenCalled()
  })

  it('envía el cambio y confirma sin sacar al usuario de la sesión', async () => {
    const usuario = userEvent.setup()
    render(<CambiarPassword />)

    await completar(usuario, 'LaActual.123', 'LaNueva.4567', 'LaNueva.4567')

    await waitFor(() =>
      expect(peticion).toHaveBeenCalledWith('/mi-cuenta/contrasena', {
        metodo: 'POST',
        cuerpo: { passwordActual: 'LaActual.123', passwordNueva: 'LaNueva.4567' },
      }),
    )

    expect(await screen.findByText('Tu contraseña se cambió correctamente.')).toBeInTheDocument()
  })

  it('marca la contraseña actual cuando el servidor dice que es incorrecta', async () => {
    peticion.mockRejectedValue(
      new ErrorHttp(403, {
        codigo: 'password_actual_incorrecta',
        mensaje: 'Tu contraseña actual no es correcta.',
      }),
    )

    const usuario = userEvent.setup()
    render(<CambiarPassword />)

    await completar(usuario, 'LaEquivocada.1', 'LaNueva.4567', 'LaNueva.4567')

    expect(await screen.findByText('Tu contraseña actual no es correcta.')).toBeInTheDocument()
    expect(screen.getByLabelText('Contraseña actual')).toHaveAttribute('aria-invalid', 'true')
  })
})
