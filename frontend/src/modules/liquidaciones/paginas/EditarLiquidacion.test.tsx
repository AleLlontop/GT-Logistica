import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { EditarLiquidacion } from './EditarLiquidacion'
import type { LiquidacionDetalle } from '../servicios/servicioLiquidaciones'

const navegar = vi.fn()

vi.mock('react-router-dom', async () => {
  const real = await vi.importActual<typeof import('react-router-dom')>('react-router-dom')

  return { ...real, useParams: () => ({ id: '12' }), useNavigate: () => navegar }
})

const obtenerLiquidacion = vi.fn()
const listarDisponibles = vi.fn()
const editarLiquidacion = vi.fn()

vi.mock('../servicios/servicioLiquidaciones', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioLiquidaciones')>(
    '../servicios/servicioLiquidaciones',
  )

  return {
    ...real,
    obtenerLiquidacion: (...args: unknown[]) => obtenerLiquidacion(...args),
    listarDisponibles: (...args: unknown[]) => listarDisponibles(...args),
    editarLiquidacion: (...args: unknown[]) => editarLiquidacion(...args),
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
    version: 3,
    viajes: [
      { id: 1, numero: 13, fecha: '2026-07-05', origen: 'Rosario', destino: 'Córdoba', importe: 120_000 },
      { id: 2, numero: 14, fecha: '2026-07-12', origen: 'Rosario', destino: 'Santa Fe', importe: 95_000 },
      { id: 3, numero: 15, fecha: '2026-07-20', origen: 'Rosario', destino: 'Paraná', importe: 140_000 },
    ],
    ordenesDePago: [],
    historial: [],
    puedeEditarse: true,
    puedeAnularse: true,
    puedeRegistrarPago: true,
    ...parcial,
  }
}

function renderizar() {
  render(
    <MemoryRouter>
      <EditarLiquidacion />
    </MemoryRouter>,
  )
}

describe('EditarLiquidacion', () => {
  beforeEach(() => {
    navegar.mockReset()
    obtenerLiquidacion.mockReset().mockResolvedValue(detalle())
    listarDisponibles.mockReset().mockResolvedValue([
      { id: 9, numero: 21, fecha: '2026-07-29', origen: 'Rosario', destino: 'Rafaela', estado: 'rendido', importe: 80_000 },
    ])
    editarLiquidacion.mockReset()
  })

  it('muestra transportista y período de sólo lectura', async () => {
    renderizar()

    const cuit = await screen.findByText('20-12345678-6')

    expect(cuit.parentElement).toHaveTextContent('Transportes Díaz — 20-12345678-6 · 07/2026')
    expect(listarDisponibles).toHaveBeenCalledWith(3, 7, 2026)
    expect(screen.queryByRole('combobox')).not.toBeInTheDocument()
  })

  /** FR-047: quitar y agregar mueven filas y anuncian el total recalculado. */
  it('quitar y agregar mueven filas y anuncian el total', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Quitar viaje #14' }))

    expect(screen.getByRole('button', { name: 'Agregar viaje #14' })).toBeInTheDocument()
    expect(screen.getByText('Importe total: $ 260.000,00 con 2 viajes.')).toHaveAttribute('role', 'status')

    await usuario.click(screen.getByRole('button', { name: 'Agregar viaje #21' }))

    expect(screen.getByRole('button', { name: 'Quitar viaje #21' })).toBeInTheDocument()
    expect(screen.getByText('Importe total: $ 340.000,00 con 3 viajes.')).toBeInTheDocument()
  })

  it('guardar está deshabilitado sin cambios y sin viajes', async () => {
    const usuario = userEvent.setup()
    renderizar()

    const guardar = await screen.findByRole('button', { name: 'Guardar cambios' })
    expect(guardar).toBeDisabled()

    await usuario.click(screen.getByRole('button', { name: 'Quitar viaje #13' }))
    await usuario.click(screen.getByRole('button', { name: 'Quitar viaje #14' }))
    await usuario.click(screen.getByRole('button', { name: 'Quitar viaje #15' }))

    expect(screen.getByText('La liquidación se quedó sin viajes. Agregá al menos uno o volvé sin guardar.')).toBeInTheDocument()
    expect(guardar).toBeDisabled()
  })

  /** FR-048: se manda la versión con que se abrió la edición. */
  it('envía el conjunto final con la versión abierta y navega al detalle', async () => {
    editarLiquidacion.mockResolvedValue(detalle({ importeTotal: 260_000, viajes: detalle().viajes.slice(0, 1).concat(detalle().viajes.slice(2)) }))
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Quitar viaje #14' }))
    await usuario.click(screen.getByRole('button', { name: 'Guardar cambios' }))

    await waitFor(() => expect(editarLiquidacion).toHaveBeenCalledWith(12, [1, 3], 3))
    expect(navegar).toHaveBeenCalledWith('/liquidaciones/12', {
      state: { aviso: 'Se guardaron los cambios de la liquidación LQ-12: ahora suma $ 260.000,00 con 2 viajes.' },
    })
  })

  it('muestra el rechazo por otra edición guardada en el medio', async () => {
    const mensaje =
      'Otro usuario guardó cambios en la liquidación LQ-12 mientras la editabas. Volvé a abrir la edición para ' +
      'ver cómo quedó y rehacer tus cambios.'
    editarLiquidacion.mockRejectedValue(new ErrorHttp(409, { codigo: 'liquidacion_modificada', mensaje }))
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Quitar viaje #14' }))
    await usuario.click(screen.getByRole('button', { name: 'Guardar cambios' }))

    expect(await screen.findByText(mensaje)).toHaveAttribute('role', 'alert')
    expect(navegar).not.toHaveBeenCalled()
  })

  /** FR-045: si al abrir ya no es editable, callout sin formulario. */
  it('muestra el bloqueo cuando al abrir ya no es editable', async () => {
    obtenerLiquidacion.mockResolvedValue(
      detalle({ importePagado: 200_000, restaPagar: 155_000, puedeEditarse: false, puedeAnularse: false }),
    )

    renderizar()

    expect(
      await screen.findByText('La liquidación LQ-12 ya tiene órdenes de pago: no se puede editar.'),
    ).toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Guardar cambios' })).not.toBeInTheDocument()
    expect(listarDisponibles).not.toHaveBeenCalled()
  })
})
