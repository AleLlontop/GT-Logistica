import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import type { RolConPermisos, UsuarioDetalle } from '../../../compartido/tipos'
import { PanelRoles } from './PanelRoles'

const obtenerUsuario = vi.fn()
const listarRoles = vi.fn()
const asignarRoles = vi.fn()

vi.mock('../servicios/usuarios', async () => {
  const real = await vi.importActual<typeof import('../servicios/usuarios')>(
    '../servicios/usuarios',
  )

  return {
    ...real,
    obtenerUsuario: (...args: unknown[]) => obtenerUsuario(...args),
    listarRoles: (...args: unknown[]) => listarRoles(...args),
    asignarRoles: (...args: unknown[]) => asignarRoles(...args),
  }
})

const usuario: UsuarioDetalle = {
  id: 7,
  username: 'jperez',
  email: 'jperez@gt.com.ar',
  estado: 'activo',
  roles: [{ codigo: 'trafico', nombre: 'Tráfico' }],
  fechaAlta: '2026-08-05T12:00:00Z',
  ultimoAcceso: null,
  persona: null,
}

const catalogo: RolConPermisos[] = [
  { codigo: 'trafico', nombre: 'Tráfico', permisosPorModulo: [] },
  { codigo: 'administracion', nombre: 'Administración de la empresa', permisosPorModulo: [] },
  { codigo: 'gerencia', nombre: 'Gerencia', permisosPorModulo: [] },
  {
    codigo: 'administrador_sistema',
    nombre: 'Administrador del sistema',
    permisosPorModulo: [
      {
        modulo: 'Usuarios',
        permisos: [
          {
            codigo: 'usuarios.gestionar',
            descripcion: 'Crear, consultar, modificar y dar de baja usuarios y sus roles',
          },
        ],
      },
    ],
  },
]

function renderizar() {
  return render(
    <MemoryRouter initialEntries={['/usuarios/7/roles']}>
      <Routes>
        <Route path="/usuarios/:id/roles" element={<PanelRoles />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('PanelRoles', () => {
  beforeEach(() => {
    obtenerUsuario.mockReset()
    listarRoles.mockReset()
    asignarRoles.mockReset()

    obtenerUsuario.mockResolvedValue(usuario)
    listarRoles.mockResolvedValue(catalogo)
    asignarRoles.mockResolvedValue(usuario)
  })

  it('muestra los cuatro roles con los que el usuario ya tiene marcados', async () => {
    renderizar()

    expect(await screen.findByLabelText('Tráfico')).toBeChecked()
    expect(screen.getByLabelText('Gerencia')).not.toBeChecked()
    expect(screen.getByLabelText('Administración de la empresa')).not.toBeChecked()
    expect(screen.getByLabelText('Administrador del sistema')).not.toBeChecked()
  })

  it('guarda exactamente la selección marcada, ni más ni menos (FR-018)', async () => {
    const persona = userEvent.setup()
    renderizar()

    await persona.click(await screen.findByLabelText('Tráfico')) // lo desmarca
    await persona.click(screen.getByLabelText('Gerencia'))

    await persona.click(screen.getByRole('button', { name: 'Guardar' }))

    await waitFor(() => expect(asignarRoles).toHaveBeenCalledWith(7, ['gerencia']))
  })

  it('rechaza guardar sin ningún rol marcado, sin llamar al servidor (FR-001)', async () => {
    const persona = userEvent.setup()
    renderizar()

    await persona.click(await screen.findByLabelText('Tráfico')) // queda ninguno
    await persona.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(
      await screen.findByText('Todo usuario tiene que tener al menos un rol asignado.'),
    ).toBeInTheDocument()

    expect(asignarRoles).not.toHaveBeenCalled()
  })

  it('muestra el motivo cuando quitar el rol dejaría al sistema sin administradores (FR-019)', async () => {
    asignarRoles.mockRejectedValue(
      new ErrorHttp(400, {
        codigo: 'ultimo_administrador',
        mensaje:
          'No se puede hacer: tiene que quedar siempre al menos un usuario activo con el rol Administrador del sistema.',
      }),
    )

    const persona = userEvent.setup()
    renderizar()

    await persona.click(await screen.findByLabelText('Gerencia'))
    await persona.click(screen.getByRole('button', { name: 'Guardar' }))

    expect(await screen.findByText(/al menos un usuario activo con el rol/)).toBeInTheDocument()
  })

  it('muestra los permisos de un rol agrupados por módulo y sin controles de edición (FR-010)', async () => {
    const persona = userEvent.setup()
    renderizar()

    const botones = await screen.findAllByRole('button', { name: 'Ver permisos' })
    await persona.click(botones[3]) // Administrador del sistema

    expect(await screen.findByRole('heading', { name: 'Usuarios' })).toBeInTheDocument()

    expect(
      screen.getByText('Crear, consultar, modificar y dar de baja usuarios y sus roles'),
    ).toBeInTheDocument()

    // Sólo lectura: el panel de permisos no agrega casillas.
    const casillas = screen.getAllByRole('checkbox')
    expect(casillas).toHaveLength(4) // las cuatro de los roles, ninguna de permisos
  })

  it('avisa cuando un rol todavía no habilita nada implementado', async () => {
    const persona = userEvent.setup()
    renderizar()

    const botones = await screen.findAllByRole('button', { name: 'Ver permisos' })
    await persona.click(botones[0]) // Tráfico, sin permisos todavía

    expect(
      await screen.findByText('Este rol todavía no habilita funcionalidades implementadas.'),
    ).toBeInTheDocument()
  })
})
