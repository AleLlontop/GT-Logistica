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

function renderizar(puedeVolverAlListado = true, puedeEmitirReportes = true) {
  return render(
    <MemoryRouter>
      <PanelVencimientos
        puedeVolverAlListado={puedeVolverAlListado}
        puedeEmitirReportes={puedeEmitirReportes}
      />
    </MemoryRouter>,
  )
}

describe('PanelVencimientos', () => {
  beforeEach(() => {
    listarVencimientos.mockReset()
    listarVencimientos.mockResolvedValue([alerta])
  })

  /**
   * El panel va bajo `choferes.vencimientos.consultar`, que también tiene Gerencia. Para ella
   * `/choferes` es un `403`, así que la salida al listado no se ofrece: un *volver* a una pantalla que
   * no se puede abrir es peor que ninguno (convención [005]).
   */
  it('no ofrece volver al listado cuando la sesión no gestiona choferes', async () => {
    renderizar(false)

    expect(await screen.findByRole('table')).toBeInTheDocument()
    expect(
      screen.queryByRole('link', { name: 'Volver al listado de choferes' }),
    ).not.toBeInTheDocument()
  })

  it('ofrece volver al listado cuando la sesión sí gestiona choferes', async () => {
    renderizar()

    expect(
      await screen.findByRole('link', { name: 'Volver al listado de choferes' }),
    ).toBeInTheDocument()
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

  // ── Módulo 12: la acción de reporte (US2) ───────────────────────────────────────────────────

  it('con el permiso ofrece Generar reporte, y la pantalla sigue sin acción primaria', async () => {
    renderizar()

    expect(await screen.findByRole('button', { name: 'Generar reporte' })).toBeEnabled()

    // No se promueve a primaria por descarte: el propósito del panel es resolver lo que vence.
    expect(screen.queryByRole('button', { name: /Nuev[ao]/ })).toBeNull()
  })

  it('sin el permiso la acción no aparece', async () => {
    renderizar(true, false)

    await screen.findByRole('table')

    expect(screen.queryByRole('button', { name: 'Generar reporte' })).toBeNull()
  })

  /** Escenario 4 de la historia: un panel sin alertas deshabilita la acción y lo explica (FR-003). */
  it('con el panel sin alertas la acción queda deshabilitada y se explica por qué', async () => {
    listarVencimientos.mockResolvedValue([])

    renderizar()

    expect(await screen.findByRole('button', { name: 'Generar reporte' })).toBeDisabled()
    expect(screen.getByText('No hay filas para reportar.')).toBeVisible()
  })
})
