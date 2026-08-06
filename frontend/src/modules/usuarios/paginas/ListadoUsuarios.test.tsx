import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import type { UsuarioListado } from '../../../compartido/tipos'
import { ListadoUsuarios } from './ListadoUsuarios'

const listarUsuarios = vi.fn()
const darDeBajaUsuario = vi.fn()

vi.mock('../servicios/usuarios', async () => {
  const real = await vi.importActual<typeof import('../servicios/usuarios')>(
    '../servicios/usuarios',
  )

  return {
    ...real,
    listarUsuarios: (...args: unknown[]) => listarUsuarios(...args),
    darDeBajaUsuario: (...args: unknown[]) => darDeBajaUsuario(...args),
  }
})

const jperez: UsuarioListado = {
  id: 1,
  username: 'jperez',
  email: 'jperez@gt.com.ar',
  estado: 'activo',
  roles: [{ codigo: 'trafico', nombre: 'Tráfico' }],
  fechaAlta: '2026-08-05T12:00:00Z',
  ultimoAcceso: null,
}

function renderizar() {
  return render(
    <MemoryRouter>
      <ListadoUsuarios />
    </MemoryRouter>,
  )
}

describe('ListadoUsuarios', () => {
  beforeEach(() => {
    listarUsuarios.mockReset()
    darDeBajaUsuario.mockReset()
    listarUsuarios.mockResolvedValue([jperez])
    darDeBajaUsuario.mockResolvedValue(undefined)
  })

  it('muestra las seis columnas de FR-011 con encabezados reales', async () => {
    renderizar()

    // Encabezados de columna reales, para que un lector de pantalla pueda anunciar cada celda.
    for (const columna of [
      'Nombre de usuario',
      'Email',
      'Estado',
      'Roles',
      'Fecha de alta',
      'Último acceso',
    ]) {
      expect(await screen.findByRole('columnheader', { name: columna })).toBeInTheDocument()
    }
  })

  it('muestra "Nunca ingresó" en vez de una celda vacía', async () => {
    renderizar()

    expect(await screen.findByText('Nunca ingresó')).toBeInTheDocument()
  })

  it('muestra un mensaje explícito cuando ningún usuario coincide (FR-012)', async () => {
    listarUsuarios.mockResolvedValue([])

    renderizar()

    expect(
      await screen.findByText('No hay usuarios que coincidan con los filtros aplicados.'),
    ).toBeInTheDocument()

    // Y no queda una tabla vacía sin explicación.
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('pide al servidor el filtro parcial que se escribe (FR-011)', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await screen.findByRole('table')

    await usuario.type(screen.getByLabelText('Nombre de usuario'), 'pere')

    await waitFor(() =>
      expect(listarUsuarios).toHaveBeenLastCalledWith(
        expect.objectContaining({ username: 'pere' }),
      ),
    )
  })

  it('pide confirmación antes de dar de baja, y cancelar no cambia nada (FR-017)', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Dar de baja' }))

    expect(await screen.findByRole('dialog')).toBeInTheDocument()
    expect(screen.getByText(/¿Confirmás la baja de jperez\?/)).toBeInTheDocument()

    await usuario.click(screen.getByRole('button', { name: 'Cancelar' }))

    expect(darDeBajaUsuario).not.toHaveBeenCalled()
  })

  it('cierra la confirmación con Escape, sin dar de baja', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Dar de baja' }))
    await screen.findByRole('dialog')

    await usuario.keyboard('{Escape}')

    await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument())
    expect(darDeBajaUsuario).not.toHaveBeenCalled()
  })

  it('muestra el motivo cuando la baja dejaría al sistema sin administradores (FR-019)', async () => {
    darDeBajaUsuario.mockRejectedValue(
      new ErrorHttp(400, {
        codigo: 'ultimo_administrador',
        mensaje:
          'No se puede hacer: tiene que quedar siempre al menos un usuario activo con el rol Administrador del sistema.',
      }),
    )

    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Dar de baja' }))
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }))

    expect(await screen.findByText(/al menos un usuario activo con el rol/)).toBeInTheDocument()
  })

  it('combina los cuatro filtros en una sola consulta', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await screen.findByRole('table')

    await usuario.type(screen.getByLabelText('Email'), 'gt.com')
    await usuario.selectOptions(screen.getByLabelText('Rol'), 'trafico')
    await usuario.selectOptions(screen.getByLabelText('Estado'), 'inactivo')

    await waitFor(() =>
      expect(listarUsuarios).toHaveBeenLastCalledWith({
        username: '',
        email: 'gt.com',
        rol: 'trafico',
        estado: 'inactivo',
      }),
    )
  })
})
