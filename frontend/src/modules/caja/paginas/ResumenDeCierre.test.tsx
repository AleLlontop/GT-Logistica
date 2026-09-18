import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { MENSAJE_CAJA_CERRADA, MENSAJE_SIN_MOVIMIENTOS, ResumenDeCierre } from './ResumenDeCierre'
import {
  MENSAJE_SIN_PERMISO,
  type MovimientoListado,
  type ResumenDeCierre as Resumen,
} from '../servicios/servicioCaja'

const navegar = vi.fn()

vi.mock('react-router-dom', async () => {
  const real = await vi.importActual<typeof import('react-router-dom')>('react-router-dom')

  return { ...real, useNavigate: () => navegar, useParams: () => ({ id: '12' }) }
})

const obtenerResumenDeCierre = vi.fn()
const cerrarCaja = vi.fn()

vi.mock('../servicios/servicioCaja', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioCaja')>('../servicios/servicioCaja')

  return {
    ...real,
    obtenerResumenDeCierre: (...args: unknown[]) => obtenerResumenDeCierre(...args),
    cerrarCaja: (...args: unknown[]) => cerrarCaja(...args),
  }
})

function movimiento(id: number, tipo: 'ingreso' | 'egreso', importe: number): MovimientoListado {
  return {
    id,
    cajaId: 12,
    fecha: '2026-09-18T12:00:00Z',
    tipo,
    importe,
    concepto: tipo === 'ingreso' ? 'Cobro flete' : 'Combustible',
    responsable: { id: 3, nombre: 'admin.empresa' },
    referencia: null,
  }
}

const RESUMEN: Resumen = {
  saldoInicial: 10_000,
  totalIngresos: 3_500,
  totalEgresos: 2_000,
  movimientos: [movimiento(1, 'ingreso', 3_500), movimiento(2, 'egreso', 2_000)],
  saldoFinal: 11_500,
}

const NUEVO: Resumen = {
  ...RESUMEN,
  totalIngresos: 4_000,
  movimientos: [...RESUMEN.movimientos, movimiento(3, 'ingreso', 500)],
  saldoFinal: 12_000,
}

const DESACTUALIZADO = 'El resumen cambió desde que lo viste. Revisalo antes de confirmar el cierre.'

function renderizar() {
  render(
    <MemoryRouter>
      <ResumenDeCierre />
    </MemoryRouter>,
  )
}

describe('ResumenDeCierre', () => {
  beforeEach(() => {
    navegar.mockReset()
    obtenerResumenDeCierre.mockReset().mockResolvedValue(RESUMEN)
    cerrarCaja.mockReset().mockResolvedValue({ tipo: 'cerrada', caja: {} })
  })

  /** CA6. */
  it('muestra saldo inicial, totales y el saldo final calculado', async () => {
    renderizar()

    expect(await screen.findByText('$ 11.500,00')).toBeInTheDocument()
    expect(screen.getByText('$ 10.000,00')).toBeInTheDocument()
    expect(screen.getAllByText('$ 3.500,00').length).toBeGreaterThan(0)
    expect(screen.getAllByText('$ 2.000,00').length).toBeGreaterThan(0)
    expect(screen.getByRole('table', { name: 'Movimientos de la caja' })).toBeInTheDocument()
  })

  /** CL1. */
  it('sin movimientos dice que el saldo final es el inicial', async () => {
    obtenerResumenDeCierre.mockResolvedValue({ ...RESUMEN, movimientos: [], totalIngresos: 0, totalEgresos: 0, saldoFinal: 10_000 })
    renderizar()

    expect(await screen.findByText(MENSAJE_SIN_MOVIMIENTOS)).toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  /** CA7. */
  it('Cancelar vuelve a la caja sin llamar al servidor', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Cancelar' }))

    expect(cerrarCaja).not.toHaveBeenCalled()
    expect(navegar).toHaveBeenCalledWith('/caja/12')
  })

  it('Confirmar cierre envía el saldo mostrado y navega a la caja', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Confirmar cierre' }))

    expect(cerrarCaja).toHaveBeenCalledWith(12, 11_500)
    expect(navegar).toHaveBeenCalledWith('/caja/12', { state: { aviso: MENSAJE_CAJA_CERRADA } })
  })

  /** US3 esc. 7: el resumen nuevo reemplaza al de la pantalla y se confirma de nuevo con su número. */
  it('un cierre desactualizado reemplaza los números, lo anuncia y no navega', async () => {
    cerrarCaja
      .mockResolvedValueOnce({ tipo: 'desactualizado', codigo: 'cierre_desactualizado', mensaje: DESACTUALIZADO, resumen: NUEVO })
      .mockResolvedValueOnce({ tipo: 'cerrada', caja: {} })
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Confirmar cierre' }))

    expect(await screen.findByRole('status')).toHaveTextContent(DESACTUALIZADO)
    expect(screen.getByText('$ 12.000,00')).toBeInTheDocument()
    expect(screen.queryByText('$ 11.500,00')).not.toBeInTheDocument()
    expect(navegar).not.toHaveBeenCalled()

    await usuario.click(screen.getByRole('button', { name: 'Confirmar cierre' }))

    expect(cerrarCaja).toHaveBeenLastCalledWith(12, 12_000)
    expect(navegar).toHaveBeenCalled()
  })

  it('un confirmacion_requerida se trata igual', async () => {
    cerrarCaja.mockResolvedValueOnce({
      tipo: 'desactualizado',
      codigo: 'confirmacion_requerida',
      mensaje: 'Revisá el resumen y confirmá el cierre.',
      resumen: NUEVO,
    })
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Confirmar cierre' }))

    expect(await screen.findByRole('status')).toHaveTextContent('Revisá el resumen y confirmá el cierre.')
    expect(screen.getByText('$ 12.000,00')).toBeInTheDocument()
    expect(navegar).not.toHaveBeenCalled()
  })

  it('una caja cerrada al confirmar se informa en una alerta', async () => {
    cerrarCaja.mockRejectedValue(
      new ErrorHttp(409, { codigo: 'caja_cerrada', mensaje: 'Esta caja ya está cerrada y no admite nuevos movimientos.' }),
    )
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Confirmar cierre' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(
      'Esta caja ya está cerrada y no admite nuevos movimientos.',
    )
  })

  it('una caja que al cargar ya no se puede cerrar lo dice, sin Confirmar cierre', async () => {
    obtenerResumenDeCierre.mockRejectedValue(
      new ErrorHttp(409, {
        codigo: 'caja_ajena',
        mensaje: 'Esta caja es de otro empleado. Sólo quien la abrió puede registrar movimientos y cerrarla.',
      }),
    )
    renderizar()

    expect(await screen.findByRole('alert')).toHaveTextContent('Esta caja es de otro empleado.')
    expect(screen.queryByRole('button', { name: 'Confirmar cierre' })).not.toBeInTheDocument()
  })

  /** Convención [010]: lo decide el `403` de la carga. */
  it('un 403 al cargar muestra el aviso sin permiso, sin resumen ni Confirmar cierre', async () => {
    obtenerResumenDeCierre.mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso.' }))
    renderizar()

    expect(await screen.findByRole('alert')).toHaveTextContent(MENSAJE_SIN_PERMISO)
    expect(screen.queryByRole('button', { name: 'Confirmar cierre' })).not.toBeInTheDocument()
    expect(screen.queryByText(/Volvé a intentar/)).not.toBeInTheDocument()
  })
})
