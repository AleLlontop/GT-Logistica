import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { AbrirCaja, MENSAJE_CAJA_ABIERTA, MENSAJE_SIN_PERMISO_PARA_ABRIR } from './AbrirCaja'
import { MENSAJE_ACCION_SIN_PERMISO, type CajaDetalle } from '../servicios/servicioCaja'

const navegar = vi.fn()

vi.mock('react-router-dom', async () => {
  const real = await vi.importActual<typeof import('react-router-dom')>('react-router-dom')

  return { ...real, useNavigate: () => navegar }
})

const abrirCaja = vi.fn()
const obtenerMiCajaAbierta = vi.fn()

vi.mock('../servicios/servicioCaja', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioCaja')>('../servicios/servicioCaja')

  return {
    ...real,
    abrirCaja: (...args: unknown[]) => abrirCaja(...args),
    obtenerMiCajaAbierta: (...args: unknown[]) => obtenerMiCajaAbierta(...args),
  }
})

const CREADA: CajaDetalle = {
  id: 12,
  responsable: { id: 3, nombre: 'admin.empresa' },
  fechaApertura: '2026-09-18T11:00:00Z',
  estado: 'abierta',
  fechaCierre: null,
  saldoFinal: null,
  saldoInicial: 10_000,
  totalIngresos: 0,
  totalEgresos: 0,
  saldoActual: 10_000,
  puedeOperar: true,
}

function renderizar(puedeGestionar = true) {
  render(
    <MemoryRouter>
      <AbrirCaja puedeGestionar={puedeGestionar} />
    </MemoryRouter>,
  )
}

describe('AbrirCaja', () => {
  beforeEach(() => {
    navegar.mockReset()
    abrirCaja.mockReset().mockResolvedValue(CREADA)
    obtenerMiCajaAbierta.mockReset().mockResolvedValue(CREADA)
  })

  it('no muestra el error antes de interactuar', () => {
    renderizar()

    expect(screen.queryByText('Escribí el saldo inicial.')).not.toBeInTheDocument()
  })

  /** RN5: guardar vacío marca el campo y no llama al servidor. */
  it('guardar vacío marca el campo y no llama a abrirCaja', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(screen.getByRole('button', { name: 'Abrir caja' }))

    expect(screen.getByText('Escribí el saldo inicial.')).toBeInTheDocument()
    expect(screen.getByLabelText('Saldo inicial')).toHaveAttribute('aria-invalid', 'true')
    expect(abrirCaja).not.toHaveBeenCalled()
  })

  /**
   * RN4: el saldo no puede ser negativo. `InputImporte` descarta el signo al escribir, así que el negativo
   * no llega a enviarse: lo rechaza además el servidor.
   */
  it('el signo menos se descarta al escribir: no se puede cargar un negativo', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(screen.getByLabelText('Saldo inicial'), '-1000')

    expect(screen.getByLabelText('Saldo inicial')).toHaveValue('1.000')
  })

  it('un saldo de cero se envía', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(screen.getByLabelText('Saldo inicial'), '0')
    await usuario.click(screen.getByRole('button', { name: 'Abrir caja' }))

    expect(abrirCaja).toHaveBeenCalledWith(0)
  })

  /** Convención [005]: el formulario no queda en pantalla. */
  it('al abrir navega a la caja con el mensaje', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(screen.getByLabelText('Saldo inicial'), '10000')
    await usuario.click(screen.getByRole('button', { name: 'Abrir caja' }))

    expect(abrirCaja).toHaveBeenCalledWith(10_000)
    expect(navegar).toHaveBeenCalledWith('/caja/12', { state: { aviso: MENSAJE_CAJA_ABIERTA } })
  })

  /** CA2: el rechazo de la segunda apertura, con el enlace a la que ya tiene y lo cargado intacto. */
  it('una caja ya abierta se informa en una alerta con el enlace a la propia', async () => {
    abrirCaja.mockRejectedValue(
      new ErrorHttp(409, {
        codigo: 'caja_ya_abierta',
        mensaje: 'Ya tenés una caja abierta. Cerrala antes de abrir otra.',
      }),
    )
    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(screen.getByLabelText('Saldo inicial'), '5000')
    await usuario.click(screen.getByRole('button', { name: 'Abrir caja' }))

    const alerta = await screen.findByRole('alert')
    expect(alerta).toHaveTextContent('Ya tenés una caja abierta. Cerrala antes de abrir otra.')
    expect(await screen.findByRole('link', { name: 'Ir a mi caja abierta' })).toHaveAttribute('href', '/caja/12')
    expect(screen.getByLabelText('Saldo inicial')).toHaveValue('5.000')
    expect(navegar).not.toHaveBeenCalled()
  })

  it('un 403 al guardar muestra el texto de acción sin permiso', async () => {
    abrirCaja.mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso.' }))
    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(screen.getByLabelText('Saldo inicial'), '5000')
    await usuario.click(screen.getByRole('button', { name: 'Abrir caja' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(MENSAJE_ACCION_SIN_PERMISO)
  })

  /** FR-033. */
  it('sin permiso muestra el aviso y ningún campo', () => {
    renderizar(false)

    expect(screen.getByRole('alert')).toHaveTextContent(MENSAJE_SIN_PERMISO_PARA_ABRIR)
    expect(screen.queryByLabelText('Saldo inicial')).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Abrir caja' })).not.toBeInTheDocument()
  })
})
