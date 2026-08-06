import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ListadoTransportistas } from './ListadoTransportistas'
import type { Transportista } from './servicioTransportistas'

const listarTransportistas = vi.fn()

vi.mock('./servicioTransportistas', async () => {
  const real = await vi.importActual<typeof import('./servicioTransportistas')>('./servicioTransportistas')
  return {
    ...real,
    listarTransportistas: (...args: unknown[]) => listarTransportistas(...args),
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
  choferesActivos: 3,
}

function renderizar() {
  return render(
    <MemoryRouter>
      <ListadoTransportistas />
    </MemoryRouter>,
  )
}

describe('ListadoTransportistas', () => {
  beforeEach(() => {
    listarTransportistas.mockReset()
    listarTransportistas.mockResolvedValue([gtlogistica])
  })

  it('avisa que el padrón está vacío, con el texto que invita a cargar el primero (FR-023)', async () => {
    listarTransportistas.mockResolvedValue([])

    renderizar()

    expect(
      await screen.findByText(
        'Todavía no hay transportistas cargados. Registrá el primero para poder asignarle choferes.',
      ),
    ).toBeInTheDocument()

    // Y no queda una tabla vacía sin explicación.
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('distingue "no hay ninguno" de "la búsqueda no encontró nada" (FR-023)', async () => {
    listarTransportistas.mockResolvedValue([])

    const usuario = userEvent.setup()
    renderizar()

    await screen.findByText(/Todavía no hay transportistas cargados/)

    await usuario.type(screen.getByLabelText('Nombre o CUIT'), 'inexistente')

    expect(
      await screen.findByText('No hay transportistas que coincidan con la búsqueda.'),
    ).toBeInTheDocument()

    expect(screen.queryByText(/Todavía no hay transportistas cargados/)).not.toBeInTheDocument()
  })

  it('formatea el CUIT y muestra cuántos choferes activos tiene cada transportista', async () => {
    renderizar()

    expect(await screen.findByText('30-71000000-6')).toBeInTheDocument()

    // La columna sale del backend (FR-010): es lo que explica por qué no se puede dar de baja.
    const fila = screen.getByRole('row', { name: /G&T Logística S\.A\./ })
    expect(fila).toHaveTextContent('3')
  })
})
