import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { DetalleLiquidacion } from './DetalleLiquidacion'
import type { LiquidacionDetalle } from '../servicios/servicioLiquidaciones'

vi.mock('react-router-dom', async () => {
  const real = await vi.importActual<typeof import('react-router-dom')>('react-router-dom')

  return { ...real, useParams: () => ({ id: '12' }), useNavigate: () => vi.fn() }
})

const obtenerLiquidacion = vi.fn()
const anularLiquidacion = vi.fn()
const registrarOrdenDePago = vi.fn()

vi.mock('../servicios/servicioLiquidaciones', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioLiquidaciones')>(
    '../servicios/servicioLiquidaciones',
  )

  return {
    ...real,
    obtenerLiquidacion: (...args: unknown[]) => obtenerLiquidacion(...args),
    anularLiquidacion: (...args: unknown[]) => anularLiquidacion(...args),
    registrarOrdenDePago: (...args: unknown[]) => registrarOrdenDePago(...args),
  }
})

function detalle(parcial: Partial<LiquidacionDetalle> = {}): LiquidacionDetalle {
  return {
    id: 12,
    numero: 'LQ-12',
    mes: 7,
    anio: 2026,
    fechaGeneracion: '2026-09-14',
    transportista: { id: 3, razonSocial: 'Transportes Díaz', cuit: '20123456786', activo: true },
    estado: 'pendiente',
    motivoAnulacion: null,
    importeTotal: 355_000,
    importePagado: 0,
    restaPagar: 355_000,
    version: 0,
    viajes: [
      { id: 1, numero: 13, fecha: '2026-07-05', origen: 'Rosario', destino: 'Córdoba', importe: 120_000 },
      { id: 2, numero: 14, fecha: '2026-07-12', origen: 'Rosario', destino: 'Santa Fe', importe: 95_000 },
      { id: 3, numero: 15, fecha: '2026-07-20', origen: 'Rosario', destino: 'Paraná', importe: 140_000 },
    ],
    ordenesDePago: [],
    historial: [{ operacion: 'generacion', usuario: 'admin.empresa', ocurridoEn: '2026-09-14T13:14:00Z', viajesQuitados: [], viajesAgregados: [] }],
    puedeEditarse: true,
    puedeAnularse: true,
    puedeRegistrarPago: true,
    ...parcial,
  }
}

function renderizar(puedeGestionar = true, aviso?: string) {
  render(
    <MemoryRouter initialEntries={[{ pathname: '/liquidaciones/12', state: aviso ? { aviso } : null }]}>
      <DetalleLiquidacion puedeGestionar={puedeGestionar} />
    </MemoryRouter>,
  )
}

describe('DetalleLiquidacion', () => {
  beforeEach(() => {
    obtenerLiquidacion.mockReset().mockResolvedValue(detalle())
    anularLiquidacion.mockReset()
    registrarOrdenDePago.mockReset()
  })

  /** US3 esc. 1 a 3: los viajes con su total, órdenes vacías y resta igual al total. */
  it('muestra una pendiente sin órdenes con lo que resta igual al total', async () => {
    renderizar()

    expect(await screen.findByText('Todavía no se registró ninguna orden de pago.')).toBeInTheDocument()
    expect(screen.getByRole('heading', { name: 'Viajes liquidados' })).toBeInTheDocument()
    expect(screen.getByText('Resta pagar')).toBeInTheDocument()
    expect(screen.getAllByText('$ 355.000,00').length).toBeGreaterThanOrEqual(2)
    expect(screen.getByText('20-12345678-6')).toBeInTheDocument()
    expect(screen.getByText(/Transportes Díaz · 07\/2026 · Generada el 14\/09\/2026/)).toBeInTheDocument()

    expect(screen.getByRole('button', { name: 'Registrar orden de pago' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Editar liquidación' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Anular liquidación' })).toBeInTheDocument()
  })

  /** FR-028, US3 esc. 6: la anulada sigue mostrando sus viajes, su motivo y ninguna acción. */
  it('muestra la anulada con los viajes que agrupaba, su motivo y sin acciones', async () => {
    obtenerLiquidacion.mockResolvedValue(
      detalle({
        estado: 'anulada',
        motivoAnulacion: 'El período no correspondía.',
        restaPagar: null,
        puedeEditarse: false,
        puedeAnularse: false,
        puedeRegistrarPago: false,
        historial: [
          { operacion: 'generacion', usuario: 'admin.empresa', ocurridoEn: '2026-09-14T13:14:00Z', viajesQuitados: [], viajesAgregados: [] },
          { operacion: 'anulacion', usuario: 'admin.empresa', ocurridoEn: '2026-09-15T13:14:00Z', viajesQuitados: [], viajesAgregados: [] },
        ],
      }),
    )

    renderizar()

    expect(await screen.findByRole('heading', { name: 'Viajes que agrupaba' })).toBeInTheDocument()
    expect(screen.getByText('Liquidación anulada.')).toBeInTheDocument()
    expect(screen.getByText('Motivo: El período no correspondía.')).toBeInTheDocument()
    expect(screen.getByText('Anulada: no hay saldo por pagar.')).toBeInTheDocument()
    expect(screen.queryByRole('button')).not.toBeInTheDocument()
  })

  it('muestra la pagada con su callout', async () => {
    obtenerLiquidacion.mockResolvedValue(
      detalle({ estado: 'pagada', importePagado: 355_000, restaPagar: 0, puedeEditarse: false, puedeAnularse: false, puedeRegistrarPago: false }),
    )

    renderizar()

    expect(await screen.findByText('Liquidación pagada — cerrada.')).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Registrar orden de pago' })).not.toBeInTheDocument()
  })

  /** FR-033: la edición dice qué viajes quitó y agregó. */
  it('muestra en el historial los viajes de una edición', async () => {
    obtenerLiquidacion.mockResolvedValue(
      detalle({
        historial: [
          { operacion: 'generacion', usuario: 'admin.empresa', ocurridoEn: '2026-09-14T13:14:00Z', viajesQuitados: [], viajesAgregados: [] },
          { operacion: 'edicion', usuario: 'admin.empresa', ocurridoEn: '2026-09-15T13:14:00Z', viajesQuitados: [13], viajesAgregados: [21] },
        ],
      }),
    )

    renderizar()

    expect(await screen.findByText('Editada por admin.empresa')).toBeInTheDocument()
    expect(screen.getByText('Quitó #13 · Agregó #21')).toBeInTheDocument()
  })

  /** FR-065: quien sólo consulta no ve acciones. */
  it('sin permiso de gestión no ofrece acciones', async () => {
    renderizar(false)

    await screen.findByText('Todavía no se registró ninguna orden de pago.')

    expect(screen.queryByRole('button', { name: 'Registrar orden de pago' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Editar liquidación' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Anular liquidación' })).not.toBeInTheDocument()
  })

  /** FR-044: la tabla de órdenes no ofrece ninguna acción por fila, ni siquiera con permiso. */
  it('las órdenes de pago no tienen acciones por fila', async () => {
    obtenerLiquidacion.mockResolvedValue(
      detalle({
        importePagado: 200_000,
        restaPagar: 155_000,
        puedeEditarse: false,
        puedeAnularse: false,
        ordenesDePago: [
          { id: 7, numero: 'OP-3', fechaPago: '2026-09-20', importe: 200_000, registradaPor: 'admin.empresa', registradaEn: '2026-09-20T14:42:00Z' },
        ],
      }),
    )

    renderizar()

    const tabla = await screen.findByRole('table', { name: 'Órdenes de pago de la liquidación LQ-12' })

    expect(within(tabla).getByText('OP-3')).toBeInTheDocument()
    expect(within(tabla).queryByRole('button')).not.toBeInTheDocument()
    expect(screen.getByText('Tiene pagos registrados — ya no se edita ni se anula.')).toBeInTheDocument()
  })

  it('anuncia el mensaje que llega por la navegación', async () => {
    renderizar(true, 'Se generó la liquidación LQ-12 por $ 355.000,00 con 3 viajes.')

    expect(
      await screen.findByText('Se generó la liquidación LQ-12 por $ 355.000,00 con 3 viajes.'),
    ).toHaveAttribute('role', 'status')
  })

  it('anula desde el diálogo y anuncia los viajes liberados', async () => {
    anularLiquidacion.mockResolvedValue(
      detalle({ estado: 'anulada', motivoAnulacion: 'Error.', restaPagar: null, puedeEditarse: false, puedeAnularse: false, puedeRegistrarPago: false }),
    )
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Anular liquidación' }))

    const dialogo = await screen.findByRole('dialog')
    await usuario.type(within(dialogo).getByLabelText('Motivo'), 'Error.')
    await usuario.click(within(dialogo).getByRole('button', { name: 'Anular liquidación' }))

    await waitFor(() => expect(anularLiquidacion).toHaveBeenCalledWith(12, 'Error.'))
    expect(
      await screen.findByText('Se anuló la liquidación LQ-12. Sus 3 viajes quedaron disponibles.'),
    ).toHaveAttribute('role', 'status')
  })
})
