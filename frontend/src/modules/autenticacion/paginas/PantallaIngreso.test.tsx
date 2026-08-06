import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { PantallaIngreso } from './PantallaIngreso'

/**
 * Cubre el recorrido con teclado que exige FR-025 y SC-008: todo el ingreso —completar los campos,
 * enviar, leer el error y reintentar— se tiene que poder hacer sin tocar el mouse.
 */
describe('PantallaIngreso', () => {
  const sesionDeEjemplo = {
    username: 'admin',
    roles: [{ codigo: 'administrador_sistema', nombre: 'Administrador del sistema' }],
    opcionesMenu: [],
  }

  function dibujar(onIngreso = vi.fn()) {
    render(
      <MemoryRouter>
        <PantallaIngreso onIngreso={onIngreso} />
      </MemoryRouter>,
    )

    return {
      onIngreso,
      usuario: screen.getByLabelText('Nombre de usuario'),
      password: screen.getByLabelText('Contraseña'),
    }
  }

  beforeEach(() => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({ ok: true, status: 200, json: async () => sesionDeEjemplo }),
    )
  })

  afterEach(() => vi.unstubAllGlobals())

  it('arranca con el foco en el nombre de usuario', () => {
    const { usuario } = dibujar()

    expect(usuario).toHaveFocus()
  })

  it('Enter en el nombre de usuario baja a la contraseña, sin enviar el formulario', async () => {
    const teclado = userEvent.setup()
    const { password } = dibujar()

    await teclado.keyboard('admin{Enter}')

    expect(password).toHaveFocus()
    expect(fetch).not.toHaveBeenCalled()
    // Y no aparece el error de campos incompletos, que era justamente lo molesto.
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('permite completar el ingreso entero con el teclado', async () => {
    const teclado = userEvent.setup()
    const { onIngreso } = dibujar()

    // Nombre de usuario, Enter, contraseña, Enter. Sin tocar el mouse en ningún momento.
    await teclado.keyboard('admin{Enter}')
    await teclado.keyboard('una-contraseña{Enter}')

    expect(fetch).toHaveBeenCalledOnce()
    expect(onIngreso).toHaveBeenCalledWith(sesionDeEjemplo)
  })

  it('también se puede recorrer con Tab, como en cualquier formulario', async () => {
    const teclado = userEvent.setup()
    const { password } = dibujar()

    await teclado.tab()

    expect(password).toHaveFocus()
  })

  it('marca los campos como obligatorios y no llama al servidor si están vacíos', async () => {
    const teclado = userEvent.setup()
    const { usuario, password } = dibujar()

    // Se envía desde el botón, porque con los campos vacíos Enter no dispara el envío.
    await teclado.click(screen.getByRole('button'))

    expect(fetch).not.toHaveBeenCalled()
    expect(usuario).toBeRequired()
    expect(password).toBeRequired()
  })

  it('el mensaje de error se anuncia y no borra lo que se había escrito', async () => {
    vi.stubGlobal(
      'fetch',
      vi.fn().mockResolvedValue({
        ok: false,
        status: 401,
        json: async () => ({
          codigo: 'credenciales_invalidas',
          mensaje: 'El usuario o la contraseña no son correctos.',
        }),
      }),
    )

    const teclado = userEvent.setup()
    const { usuario } = dibujar()

    await teclado.keyboard('admin{Enter}')
    await teclado.keyboard('mal{Enter}')

    // role="alert" es lo que hace que un lector de pantalla lo lea al aparecer.
    const error = await screen.findByRole('alert')
    expect(error).toHaveTextContent('El usuario o la contraseña no son correctos.')

    // Se puede reintentar de inmediato: el usuario escrito sigue ahí.
    expect(usuario).toHaveValue('admin')
  })
})
