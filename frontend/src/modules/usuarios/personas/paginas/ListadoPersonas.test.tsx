import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../../compartido/clienteHttp'
import type { Persona } from '../../../../compartido/tipos'
import { ListadoPersonas } from './ListadoPersonas'

const listarPersonas = vi.fn()
const darDeBajaPersona = vi.fn()

vi.mock('../servicios/personas', async () => {
  const real = await vi.importActual<typeof import('../servicios/personas')>('../servicios/personas')

  return {
    ...real,
    listarPersonas: (...args: unknown[]) => listarPersonas(...args),
    darDeBajaPersona: (...args: unknown[]) => darDeBajaPersona(...args),
  }
})

const marta: Persona = {
  id: 1,
  nombre: 'Marta',
  apellido: 'Gómez',
  dni: '28777666',
  tipo: 'chofer',
  telefono: '11-5555-5555',
  email: 'marta@gt.com.ar',
  fechaNacimiento: '1985-03-12',
  activa: true,
}

function renderizar() {
  return render(
    <MemoryRouter>
      <ListadoPersonas />
    </MemoryRouter>,
  )
}

describe('ListadoPersonas', () => {
  beforeEach(() => {
    listarPersonas.mockReset()
    darDeBajaPersona.mockReset()
    listarPersonas.mockResolvedValue([marta])
    darDeBajaPersona.mockResolvedValue(undefined)
  })

  it('avisa que el padrón está vacío cuando todavía no se cargó ninguna persona (FR-025)', async () => {
    listarPersonas.mockResolvedValue([])

    renderizar()

    expect(
      await screen.findByText(
        'Todavía no hay personas cargadas. Registrá la primera para poder asociarla a un usuario.',
      ),
    ).toBeInTheDocument()
  })

  it('usa un mensaje distinto cuando la búsqueda no encuentra nada', async () => {
    // Son dos situaciones distintas y llevan a acciones distintas: cargar la primera persona, o
    // corregir la búsqueda.
    const usuario = userEvent.setup()
    renderizar()

    await screen.findByRole('table')

    listarPersonas.mockResolvedValue([])
    await usuario.type(screen.getByLabelText('Buscar por nombre, apellido o DNI'), 'zzz')

    expect(
      await screen.findByText('No hay personas que coincidan con la búsqueda.'),
    ).toBeInTheDocument()

    expect(
      screen.queryByText(/Todavía no hay personas cargadas/),
    ).not.toBeInTheDocument()
  })

  it('muestra las columnas del padrón con encabezados reales', async () => {
    renderizar()

    for (const columna of ['Nombre', 'Apellido', 'DNI', 'Tipo', 'Teléfono', 'Email', 'Estado']) {
      expect(await screen.findByRole('columnheader', { name: columna })).toBeInTheDocument()
    }
  })

  it('pide confirmación antes de dar de baja, y cancelar no cambia nada (FR-017)', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Dar de baja' }))

    expect(await screen.findByRole('dialog')).toBeInTheDocument()

    await usuario.click(screen.getByRole('button', { name: 'Cancelar' }))

    expect(darDeBajaPersona).not.toHaveBeenCalled()
  })

  it('muestra el motivo cuando la persona está vinculada a un usuario (FR-028)', async () => {
    darDeBajaPersona.mockRejectedValue(
      new ErrorHttp(400, {
        codigo: 'persona_vinculada',
        mensaje: 'No se puede dar de baja: está asociada al usuario jperez. Desvinculala primero.',
      }),
    )

    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Dar de baja' }))
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }))

    await waitFor(() => expect(darDeBajaPersona).toHaveBeenCalledWith(marta.id))

    expect(
      await screen.findByText(
        'No se puede dar de baja: está asociada al usuario jperez. Desvinculala primero.',
      ),
    ).toBeInTheDocument()
  })
})
