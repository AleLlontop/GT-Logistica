import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { PanelVencimientos } from './PanelVencimientos'
import type { AlertaVencimiento } from '../servicios/servicioChoferes'

const listarVencimientos = vi.fn()

vi.mock('../servicios/servicioChoferes', async () => {
  const real = await vi.importActual<
    typeof import('../servicios/servicioChoferes')
  >('../servicios/servicioChoferes')
  return { ...real, listarVencimientos: (...args: unknown[]) => listarVencimientos(...args) }
})

const alerta: AlertaVencimiento = {
  choferId: 7,
  apellido: 'Gómez',
  nombre: 'Ramona',
  transportista: { id: 1, nombre: 'G&T Logística S.A.' },
  documento: {
    id: 3,
    tipo: { id: 1, nombre: 'Licencia de conducir' },
    numero: 'LIC-999',
    fechaEmision: '2020-01-01',
    fechaVencimiento: '2026-07-30',
    estado: 'vencida',
    esVigenteDelTipo: true,
    diasHastaVencimiento: -7,
    tieneArchivo: false,
    archivoNombre: null,
  },
}

function renderizar() {
  return render(
    <MemoryRouter>
      <PanelVencimientos />
    </MemoryRouter>,
  )
}

describe('PanelVencimientos', () => {
  beforeEach(() => {
    listarVencimientos.mockReset()
    listarVencimientos.mockResolvedValue([alerta])
  })

  /** US5 esc. 4: una lista vacía es una buena noticia y se dice, no se muestra una tabla vacía. */
  it('informa explícitamente que no hay vencimientos pendientes (US5 esc. 4)', async () => {
    listarVencimientos.mockResolvedValue([])

    renderizar()

    expect(
      await screen.findByText('No hay documentación próxima a vencer ni vencida.'),
    ).toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('muestra cuántos días pasaron y lleva a la ficha del chofer (US5 esc. 2)', async () => {
    renderizar()

    expect(await screen.findByText(/Venció hace 7 días/)).toBeInTheDocument()

    expect(screen.getByRole('link', { name: 'Gómez, Ramona' })).toHaveAttribute(
      'href',
      '/choferes/7',
    )
  })

  it('acompaña el estado con texto, no sólo con color', async () => {
    renderizar()

    await screen.findByRole('table')

    expect(screen.getByText(/Vencida/)).toBeInTheDocument()
  })
})
