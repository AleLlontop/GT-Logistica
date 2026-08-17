import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { PanelVencimientos } from './PanelVencimientos'
import { situacion } from '../servicios/api'
import { TotalesFacturados } from './TotalesFacturados'
import type { FilaDeVencimiento, TotalPorCliente } from '../servicios/servicioFacturas'

const consultarVencimientos = vi.fn()
const consultarTotalesFacturados = vi.fn()

vi.mock('../servicios/servicioFacturas', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioFacturas')>(
    '../servicios/servicioFacturas',
  )

  return {
    ...real,
    consultarVencimientos: () => consultarVencimientos(),
    consultarTotalesFacturados: (...args: unknown[]) => consultarTotalesFacturados(...args),
  }
})

function fila(parcial: Partial<FilaDeVencimiento> & { id: number }): FilaDeVencimiento {
  return {
    numeroComprobante: `0014-0000000${parcial.id}`,
    cliente: 'Distribuidora del Litoral',
    total: 121_000,
    vencimientoPago: '2026-09-11',
    dias: 5,
    ...parcial,
  }
}

describe('situacion', () => {
  /**
   * FR-063 y FR-065: la situación va **con la palabra**, no sólo con un color. El cero tiene su propio
   * texto: `Vence en 0 días` sería técnicamente correcto y no es lo que nadie diría.
   */
  it('dice la situación con palabras', () => {
    expect(situacion(-3)).toBe('Vencida hace 3 días')
    expect(situacion(-1)).toBe('Vencida hace 1 día')
    expect(situacion(0)).toBe('Vence hoy')
    expect(situacion(1)).toBe('Vence en 1 día')
    expect(situacion(5)).toBe('Vence en 5 días')
  })
})

describe('PanelVencimientos', () => {
  beforeEach(() => {
    consultarVencimientos.mockReset().mockResolvedValue([fila({ id: 1 })])
  })

  it('muestra las cinco columnas y la situación en palabras', async () => {
    consultarVencimientos.mockResolvedValue([
      fila({ id: 1, dias: -3 }),
      fila({ id: 2, dias: 0 }),
      fila({ id: 3, dias: 5 }),
    ])

    render(
      <MemoryRouter>
        <PanelVencimientos />
      </MemoryRouter>,
    )

    expect(await screen.findByText('Vencida hace 3 días')).toBeInTheDocument()
    expect(screen.getByText('Vence hoy')).toBeInTheDocument()
    expect(screen.getByText('Vence en 5 días')).toBeInTheDocument()

    expect(screen.getByRole('columnheader', { name: 'Cliente' })).toBeInTheDocument()
    expect(screen.getByRole('columnheader', { name: 'Número' })).toBeInTheDocument()
    expect(screen.getByRole('columnheader', { name: 'Importe' })).toBeInTheDocument()
    expect(screen.getByRole('columnheader', { name: 'Vencimiento' })).toBeInTheDocument()
    expect(screen.getByRole('columnheader', { name: 'Situación' })).toBeInTheDocument()
  })

  /** Un panel vacío es una respuesta legítima, y se dice con esas palabras (FR-063). */
  it('con el panel vacío lo dice en vez de mostrar una tabla sin filas', async () => {
    consultarVencimientos.mockResolvedValue([])

    render(
      <MemoryRouter>
        <PanelVencimientos />
      </MemoryRouter>,
    )

    expect(
      await screen.findByText('No hay facturas vencidas ni por vencer en los próximos 7 días.'),
    ).toBeInTheDocument()

    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('formatea los importes en pesos y las fechas en dd/MM/yyyy', async () => {
    consultarVencimientos.mockResolvedValue([fila({ id: 1, total: 1_240_000 })])

    render(
      <MemoryRouter>
        <PanelVencimientos />
      </MemoryRouter>,
    )

    expect(await screen.findByText('$ 1.240.000,00')).toBeInTheDocument()
    expect(screen.getByText('11/09/2026')).toBeInTheDocument()
  })
})

describe('TotalesFacturados', () => {
  beforeEach(() => {
    consultarTotalesFacturados.mockReset().mockResolvedValue([
      {
        clienteId: 7,
        razonSocial: 'Distribuidora del Litoral',
        cantidad: 3,
        facturado: 363_000,
        cobrado: 121_000,
        pendiente: 242_000,
      } satisfies TotalPorCliente,
    ])
  })

  /**
   * FR-061: **sin rango elegido no calcula ni muestra nada, y lo dice.** Un cuadro vacío se leería como
   * "no hay facturas", que es una respuesta distinta de "todavía no me dijiste qué período mirar".
   */
  it('sin rango no calcula nada y lo dice', async () => {
    render(<TotalesFacturados />)

    expect(
      await screen.findByText('Elegí un rango de fechas para ver los totales.'),
    ).toBeInTheDocument()

    expect(consultarTotalesFacturados).not.toHaveBeenCalled()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('deshabilita el botón mientras el rango esté incompleto', async () => {
    const { default: userEvent } = await import('@testing-library/user-event')
    const usuario = userEvent.setup()

    render(<TotalesFacturados />)

    expect(screen.getByRole('button', { name: 'Ver totales' })).toBeDisabled()

    await usuario.type(screen.getByLabelText('Desde'), '2026-08-01')
    expect(screen.getByRole('button', { name: 'Ver totales' })).toBeDisabled()

    await usuario.type(screen.getByLabelText('Hasta'), '2026-08-31')
    expect(screen.getByRole('button', { name: 'Ver totales' })).toBeEnabled()
  })

  it('muestra las cinco columnas y la nota de que las anuladas no suman', async () => {
    const { default: userEvent } = await import('@testing-library/user-event')
    const usuario = userEvent.setup()

    render(<TotalesFacturados />)

    await usuario.type(screen.getByLabelText('Desde'), '2026-08-01')
    await usuario.type(screen.getByLabelText('Hasta'), '2026-08-31')
    await usuario.click(screen.getByRole('button', { name: 'Ver totales' }))

    expect(await screen.findByText('Distribuidora del Litoral')).toBeInTheDocument()
    expect(screen.getByText('$ 363.000,00')).toBeInTheDocument()
    expect(screen.getByText('$ 121.000,00')).toBeInTheDocument()
    expect(screen.getByText('$ 242.000,00')).toBeInTheDocument()

    expect(
      screen.getByText(
        'Las facturas anuladas no suman en ninguna columna. La fecha de corte es la fecha de ' +
          'facturación.',
      ),
    ).toBeInTheDocument()
  })

  it('sin resultados nombra el rango consultado', async () => {
    consultarTotalesFacturados.mockResolvedValue([])

    const { default: userEvent } = await import('@testing-library/user-event')
    const usuario = userEvent.setup()

    render(<TotalesFacturados />)

    await usuario.type(screen.getByLabelText('Desde'), '2026-08-01')
    await usuario.type(screen.getByLabelText('Hasta'), '2026-08-31')
    await usuario.click(screen.getByRole('button', { name: 'Ver totales' }))

    expect(
      await screen.findByText('No hay facturas emitidas entre el 01/08/2026 y el 31/08/2026.'),
    ).toBeInTheDocument()
  })
})
