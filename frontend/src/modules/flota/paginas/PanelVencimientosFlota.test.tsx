import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { PanelVencimientosFlota } from './PanelVencimientosFlota'
import type { AlertaVencimientoFlota } from '../servicios/servicioFlota'

const listarVencimientosDeFlota = vi.fn()

vi.mock('../servicios/servicioFlota', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioFlota')>(
    '../servicios/servicioFlota',
  )
  return {
    ...real,
    listarVencimientosDeFlota: (...args: unknown[]) => listarVencimientosDeFlota(...args),
  }
})

function alerta(diasHastaVencimiento: number): AlertaVencimientoFlota {
  return {
    vehiculoId: 1,
    patente: 'AB123CD',
    transportista: { id: 1, nombre: 'G&T Logística S.A.' },
    documento: {
      id: 5,
      vehiculoId: 1,
      tipo: { id: 10, nombre: 'Seguro' },
      numero: 'POL-123',
      fechaEmision: '2025-01-10',
      fechaVencimiento: '2026-08-01',
      estado: diasHastaVencimiento < 0 ? 'vencida' : 'proximaAvencer',
      esVigenteDelTipo: true,
      diasHastaVencimiento,
      tieneArchivo: false,
      archivoNombre: null,
    },
  }
}

function renderizar() {
  return render(
    <MemoryRouter>
      <PanelVencimientosFlota />
    </MemoryRouter>,
  )
}

describe('PanelVencimientosFlota', () => {
  beforeEach(() => {
    listarVencimientosDeFlota.mockReset()
    listarVencimientosDeFlota.mockResolvedValue([])
  })

  /** US5 esc. 5 y FR-036: una lista vacía es una buena noticia, y se dice. */
  it('avisa que no hay vencimientos pendientes, con el texto del contrato (US5 esc. 5)', async () => {
    renderizar()

    expect(await screen.findByText('No hay vencimientos pendientes.')).toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  /** FR-035: "Venció hace N días" para lo vencido. */
  it('dice hace cuántos días venció un documento (FR-035)', async () => {
    listarVencimientosDeFlota.mockResolvedValue([alerta(-7)])

    renderizar()

    const tabla = await screen.findByRole('table')

    expect(tabla).toHaveTextContent('Venció hace 7 días')
    expect(tabla).toHaveTextContent('Vencida')
  })

  /** Y "Vence en N días" para lo que está por vencer. */
  it('dice en cuántos días vence un documento próximo (FR-035)', async () => {
    listarVencimientosDeFlota.mockResolvedValue([alerta(12)])

    renderizar()

    expect(await screen.findByRole('table')).toHaveTextContent('Vence en 12 días')
  })

  /** El singular no se escribe en plural: un día es "1 día". */
  it('usa el singular cuando falta un solo día', async () => {
    listarVencimientosDeFlota.mockResolvedValue([alerta(1)])

    renderizar()

    expect(await screen.findByRole('table')).toHaveTextContent('Vence en 1 día')
  })

  /** US5 esc. 2: cada fila lleva a la ficha de la unidad. */
  it('enlaza cada fila con la ficha de su unidad (US5 esc. 2)', async () => {
    listarVencimientosDeFlota.mockResolvedValue([alerta(-3)])

    renderizar()

    const enlace = await screen.findByRole('link', { name: 'AB123CD' })

    expect(enlace).toHaveAttribute('href', '/flota/1')
  })
})
