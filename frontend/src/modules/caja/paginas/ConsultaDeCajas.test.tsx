import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { formatearInstante } from '../../../compartido/fechas'
import { ConsultaDeCajas } from './ConsultaDeCajas'
import { MENSAJE_SIN_PERMISO, type CajaDetalle, type CajaListado, type PaginaDeCajas } from '../servicios/servicioCaja'

const listarCajas = vi.fn()
const obtenerMiCajaAbierta = vi.fn()

vi.mock('../servicios/servicioCaja', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioCaja')>('../servicios/servicioCaja')

  return {
    ...real,
    listarCajas: (...args: unknown[]) => listarCajas(...args),
    obtenerMiCajaAbierta: (...args: unknown[]) => obtenerMiCajaAbierta(...args),
  }
})

const ABIERTA: CajaListado = {
  id: 2,
  responsable: { id: 3, nombre: 'admin.empresa' },
  fechaApertura: '2026-09-18T11:00:00Z',
  estado: 'abierta',
  fechaCierre: null,
  saldoFinal: null,
}

const CERRADA: CajaListado = {
  id: 1,
  responsable: { id: 4, nombre: 'cajera' },
  fechaApertura: '2026-09-17T11:00:00Z',
  estado: 'cerrada',
  fechaCierre: '2026-09-17T21:00:00Z',
  saldoFinal: 11_500,
}

function pagina(items: CajaListado[]): PaginaDeCajas {
  return { items, total: items.length, pagina: 1, tamanioPagina: 20 }
}

const PROPIA = { ...ABIERTA, saldoInicial: 0, totalIngresos: 0, totalEgresos: 0, saldoActual: 0, puedeOperar: true } satisfies CajaDetalle

function renderizar(puedeGestionar = true) {
  render(
    <MemoryRouter>
      <ConsultaDeCajas puedeGestionar={puedeGestionar} />
    </MemoryRouter>,
  )
}

describe('ConsultaDeCajas', () => {
  beforeEach(() => {
    listarCajas.mockReset().mockResolvedValue(pagina([ABIERTA, CERRADA]))
    obtenerMiCajaAbierta.mockReset().mockResolvedValue(null)
  })

  /** FR-026, FR-029: la palabra del estado, y cierre y saldo final sólo en la cerrada. */
  it('muestra las columnas de una abierta y una cerrada', async () => {
    renderizar()

    const tabla = await screen.findByRole('table', { name: 'Cajas' })
    const [, filaAbierta, filaCerrada] = within(tabla).getAllByRole('row')

    expect(within(filaAbierta).getByText('Abierta')).toBeInTheDocument()
    expect(within(filaAbierta).queryByText(/\$/)).not.toBeInTheDocument()

    expect(within(filaCerrada).getByText('Cerrada')).toBeInTheDocument()
    expect(within(filaCerrada).getByText('$ 11.500,00')).toBeInTheDocument()
    expect(within(filaCerrada).getByText(formatearInstante('2026-09-17T21:00:00Z'))).toBeInTheDocument()
  })

  it('con permiso y sin caja propia ofrece Abrir caja', async () => {
    renderizar()

    expect(await screen.findByRole('link', { name: 'Abrir caja' })).toHaveAttribute('href', '/caja/nueva')
  })

  /** RN1: con una caja abierta no se ofrece abrir otra, sino ir a ella. */
  it('con una caja propia abierta ofrece ir a ella', async () => {
    obtenerMiCajaAbierta.mockResolvedValue(PROPIA)
    renderizar()

    expect(await screen.findByRole('link', { name: 'Ir a mi caja abierta' })).toHaveAttribute('href', '/caja/2')
    expect(screen.queryByRole('link', { name: 'Abrir caja' })).not.toBeInTheDocument()
  })

  /** US4 esc. 7: quien sólo consulta no ve botones de operación. */
  it('quien sólo consulta no ve ningún botón', async () => {
    renderizar(false)

    await screen.findByRole('table', { name: 'Cajas' })
    expect(screen.queryByRole('link', { name: 'Abrir caja' })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Ir a mi caja abierta' })).not.toBeInTheDocument()
    expect(obtenerMiCajaAbierta).not.toHaveBeenCalled()
  })

  /** Convención [010]. */
  it('un 403 al cargar muestra el aviso sin permiso y no el error de carga', async () => {
    listarCajas.mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso.' }))
    renderizar(false)

    expect(await screen.findByRole('alert')).toHaveTextContent(MENSAJE_SIN_PERMISO)
    expect(screen.queryByText(/Volvé a intentar/)).not.toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })
})
