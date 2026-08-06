import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { FormularioUsuario } from './FormularioUsuario'

const crearUsuario = vi.fn()

vi.mock('../servicios/usuarios', async () => {
  const real = await vi.importActual<typeof import('../servicios/usuarios')>(
    '../servicios/usuarios',
  )

  return { ...real, crearUsuario: (...args: unknown[]) => crearUsuario(...args) }
})

// El padrón arranca vacío, que es el estado de toda instalación nueva (FR-024).
vi.mock('../personas/servicios/personas', () => ({
  listarPersonas: () => Promise.resolve([]),
  nombreCompleto: (persona: { nombre: string; apellido: string }) =>
    `${persona.apellido}, ${persona.nombre}`,
}))

function renderizar() {
  return render(
    <MemoryRouter>
      <FormularioUsuario />
    </MemoryRouter>,
  )
}

describe('FormularioUsuario (alta)', () => {
  beforeEach(() => {
    crearUsuario.mockReset()
    crearUsuario.mockResolvedValue({ id: 1 })
  })

  it('precarga el estado en activo (FR-005)', async () => {
    renderizar()

    const estado = await screen.findByLabelText('Estado')

    expect(estado).toHaveValue('activo')
  })

  it('rechaza el alta sin ningún rol marcado y no llama al servidor (FR-001)', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(screen.getByLabelText('Nombre de usuario'), 'jperez')
    await usuario.type(screen.getByLabelText('Email'), 'jperez@gt.com.ar')
    await usuario.type(screen.getByLabelText('Contraseña inicial'), 'Password.1234')

    await usuario.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(
      await screen.findByText('Todo usuario tiene que tener al menos un rol asignado.'),
    ).toBeInTheDocument()

    expect(crearUsuario).not.toHaveBeenCalled()
  })

  it('marca el email inválido en su propio campo, sin llamar al servidor', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(screen.getByLabelText('Nombre de usuario'), 'jperez')
    await usuario.type(screen.getByLabelText('Email'), 'esto-no-es-un-mail')
    await usuario.type(screen.getByLabelText('Contraseña inicial'), 'Password.1234')
    await usuario.click(screen.getByLabelText('Tráfico'))

    await usuario.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(
      await screen.findByText('Escribí un email válido, con formato nombre@dominio.'),
    ).toBeInTheDocument()

    expect(screen.getByLabelText('Email')).toHaveAttribute('aria-invalid', 'true')
    expect(crearUsuario).not.toHaveBeenCalled()
  })

  it('rechaza una contraseña de menos de 8 caracteres (FR-004)', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(screen.getByLabelText('Nombre de usuario'), 'jperez')
    await usuario.type(screen.getByLabelText('Email'), 'jperez@gt.com.ar')
    await usuario.type(screen.getByLabelText('Contraseña inicial'), '1234567')
    await usuario.click(screen.getByLabelText('Tráfico'))

    await usuario.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(
      await screen.findByText('La contraseña tiene que tener al menos 8 caracteres.'),
    ).toBeInTheDocument()

    expect(crearUsuario).not.toHaveBeenCalled()
  })

  it('envía el alta cuando los datos son válidos', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(screen.getByLabelText('Nombre de usuario'), 'jperez')
    await usuario.type(screen.getByLabelText('Email'), 'jperez@gt.com.ar')
    await usuario.type(screen.getByLabelText('Contraseña inicial'), 'Password.1234')
    await usuario.click(screen.getByLabelText('Tráfico'))

    await usuario.click(screen.getByRole('button', { name: 'Guardar' }))

    await waitFor(() => expect(crearUsuario).toHaveBeenCalledTimes(1))

    expect(crearUsuario).toHaveBeenCalledWith({
      username: 'jperez',
      email: 'jperez@gt.com.ar',
      password: 'Password.1234',
      estado: 'activo',
      roles: ['trafico'],
      personaId: null,
    })
  })

  it('avisa que el padrón está vacío en vez de mostrar un desplegable sin opciones', async () => {
    renderizar()

    expect(
      await screen.findByText(/No hay personas registradas/),
    ).toBeInTheDocument()
  })
})
