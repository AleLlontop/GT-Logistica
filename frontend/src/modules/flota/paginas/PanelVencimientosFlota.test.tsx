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

function renderizar(puedeVolverAlListado = true, puedeEmitirReportes = true) {
  return render(
    <MemoryRouter>
      <PanelVencimientosFlota
        puedeVolverAlListado={puedeVolverAlListado}
        puedeEmitirReportes={puedeEmitirReportes}
      />
    </MemoryRouter>,
  )
}

describe('PanelVencimientosFlota', () => {
  beforeEach(() => {
    listarVencimientosDeFlota.mockReset()
    listarVencimientosDeFlota.mockResolvedValue([])
  })

  /** El mismo caso que el panel de choferes: Gerencia llega acá y `/flota` le responde `403`. */
  it('no ofrece volver al listado cuando la sesión no gestiona la flota', async () => {
    renderizar(false)

    expect(await screen.findByText('No hay vencimientos pendientes.')).toBeInTheDocument()
    expect(
      screen.queryByRole('link', { name: 'Volver al listado de flota' }),
    ).not.toBeInTheDocument()
  })

  it('ofrece volver al listado cuando la sesión sí gestiona la flota', async () => {
    renderizar()

    expect(
      await screen.findByRole('link', { name: 'Volver al listado de flota' }),
    ).toBeInTheDocument()
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

  // ── Módulo 12: la acción de reporte (US2) ───────────────────────────────────────────────────

  it('con el permiso ofrece Generar reporte, y la pantalla sigue sin acción primaria', async () => {
    listarVencimientosDeFlota.mockResolvedValue([alerta(-3)])

    renderizar()

    expect(await screen.findByRole('button', { name: 'Generar reporte' })).toBeEnabled()

    // No se promueve a primaria por descarte: el propósito del panel es resolver lo que vence.
    expect(screen.queryByRole('button', { name: /Nuev[ao]/ })).toBeNull()
  })

  it('sin el permiso la acción no aparece', async () => {
    listarVencimientosDeFlota.mockResolvedValue([alerta(-3)])

    renderizar(true, false)

    await screen.findByRole('table')

    expect(screen.queryByRole('button', { name: 'Generar reporte' })).toBeNull()
  })

  /** Escenario 4 de la historia: un panel sin alertas deshabilita la acción y lo explica (FR-003). */
  it('con el panel sin alertas la acción queda deshabilitada y se explica por qué', async () => {
    listarVencimientosDeFlota.mockResolvedValue([])

    renderizar()

    expect(await screen.findByRole('button', { name: 'Generar reporte' })).toBeDisabled()
    expect(screen.getByText('No hay filas para reportar.')).toBeVisible()
  })
})
