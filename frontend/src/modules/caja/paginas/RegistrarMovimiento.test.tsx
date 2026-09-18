import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import {
  AVISO_TOPE_DE_ORDENES,
  MENSAJE_MOVIMIENTO_REGISTRADO,
  RegistrarMovimiento,
} from './RegistrarMovimiento'
import {
  MENSAJE_ACCION_SIN_PERMISO,
  MENSAJE_SIN_CAJA_ABIERTA,
  type CajaDetalle,
  type MovimientoListado,
} from '../servicios/servicioCaja'

const navegar = vi.fn()

vi.mock('react-router-dom', async () => {
  const real = await vi.importActual<typeof import('react-router-dom')>('react-router-dom')

  return { ...real, useNavigate: () => navegar, useParams: () => ({ id: '12' }) }
})

const obtenerCaja = vi.fn()
const listarFacturasPendientes = vi.fn()
const listarOrdenesDePago = vi.fn()
const registrarMovimiento = vi.fn()

vi.mock('../servicios/servicioCaja', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioCaja')>('../servicios/servicioCaja')

  return {
    ...real,
    obtenerCaja: (...args: unknown[]) => obtenerCaja(...args),
    listarFacturasPendientes: (...args: unknown[]) => listarFacturasPendientes(...args),
    listarOrdenesDePago: (...args: unknown[]) => listarOrdenesDePago(...args),
    registrarMovimiento: (...args: unknown[]) => registrarMovimiento(...args),
  }
})

function caja(parcial: Partial<CajaDetalle> = {}): CajaDetalle {
  return {
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
    ...parcial,
  }
}

const REGISTRADO: MovimientoListado = {
  id: 40,
  cajaId: 12,
  fecha: '2026-09-18T12:00:00Z',
  tipo: 'ingreso',
  importe: 3_500,
  concepto: 'Cobro flete',
  responsable: { id: 3, nombre: 'admin.empresa' },
  referencia: null,
}

function renderizar(puedeGestionar = true) {
  render(
    <MemoryRouter>
      <RegistrarMovimiento puedeGestionar={puedeGestionar} />
    </MemoryRouter>,
  )
}

async function completar(usuario: ReturnType<typeof userEvent.setup>) {
  await usuario.click(await screen.findByRole('button', { name: 'Ingreso' }))
  await usuario.type(screen.getByLabelText('Importe'), '3500')
  await usuario.type(screen.getByLabelText('Concepto'), 'Cobro flete')
}

describe('RegistrarMovimiento', () => {
  beforeEach(() => {
    navegar.mockReset()
    obtenerCaja.mockReset().mockResolvedValue(caja())
    listarFacturasPendientes.mockReset().mockResolvedValue([{ id: 7, texto: 'Factura 0014-00000003 · Distribuidora' }])
    listarOrdenesDePago.mockReset().mockResolvedValue([{ id: 9, texto: 'OP-4 · LQ-2 · $ 100.000,00' }])
    registrarMovimiento.mockReset().mockResolvedValue(REGISTRADO)
  })

  it('guardar vacío marca tipo, importe y concepto y no llama al servidor', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Guardar movimiento' }))

    expect(screen.getByText('Elegí si es un ingreso o un egreso.')).toBeInTheDocument()
    expect(screen.getByText('Escribí un importe mayor que cero.')).toBeInTheDocument()
    expect(screen.getByText('Escribí el concepto del movimiento.')).toBeInTheDocument()
    expect(registrarMovimiento).not.toHaveBeenCalled()
  })

  it('un importe cero se marca', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(await screen.findByLabelText('Importe'), '0')
    await usuario.tab()

    expect(screen.getByText('Escribí un importe mayor que cero.')).toBeInTheDocument()
  })

  /** `InputImporte` no deja escribir un tercer decimal: se corta en dos. */
  it('el importe no admite más de dos decimales', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(await screen.findByLabelText('Importe'), '150,555')

    expect(screen.getByLabelText('Importe')).toHaveValue('150,55')
  })

  it('un concepto de sólo espacios se marca', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(await screen.findByLabelText('Concepto'), '   ')
    await usuario.tab()

    expect(screen.getByText('Escribí el concepto del movimiento.')).toBeInTheDocument()
  })

  /** FR-011: la referencia depende del tipo, con el tope declarado sólo en egreso. */
  it('Ingreso ofrece facturas y Egreso órdenes, con el tope sólo en Egreso', async () => {
    const usuario = userEvent.setup()
    renderizar()

    expect(await screen.findByLabelText('Referencia')).toBeDisabled()

    await usuario.click(screen.getByRole('button', { name: 'Ingreso' }))
    expect(await screen.findByRole('option', { name: 'Factura 0014-00000003 · Distribuidora' })).toBeInTheDocument()
    expect(screen.queryByText(AVISO_TOPE_DE_ORDENES)).not.toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Ingreso' })).toHaveAttribute('aria-pressed', 'true')

    await usuario.click(screen.getByRole('button', { name: 'Egreso' }))
    expect(await screen.findByRole('option', { name: 'OP-4 · LQ-2 · $ 100.000,00' })).toBeInTheDocument()
    expect(screen.getByText(AVISO_TOPE_DE_ORDENES)).toBeInTheDocument()
  })

  it('cambiar el tipo vuelve a Sin referencia y conserva importe y concepto', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await completar(usuario)
    await screen.findByRole('option', { name: 'Factura 0014-00000003 · Distribuidora' })
    await usuario.selectOptions(screen.getByLabelText('Referencia'), '7')

    await usuario.click(screen.getByRole('button', { name: 'Egreso' }))

    expect(screen.getByLabelText('Referencia')).toHaveValue('')
    expect(screen.getByLabelText('Importe')).toHaveValue('3.500')
    expect(screen.getByLabelText('Concepto')).toHaveValue('Cobro flete')
  })

  it('envía la factura como referencia de un ingreso y navega a la caja', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await completar(usuario)
    await screen.findByRole('option', { name: 'Factura 0014-00000003 · Distribuidora' })
    await usuario.selectOptions(screen.getByLabelText('Referencia'), '7')
    await usuario.click(screen.getByRole('button', { name: 'Guardar movimiento' }))

    expect(registrarMovimiento).toHaveBeenCalledWith(12, {
      tipo: 'ingreso',
      importe: 3_500,
      concepto: 'Cobro flete',
      facturaId: 7,
    })
    expect(navegar).toHaveBeenCalledWith('/caja/12', { state: { aviso: MENSAJE_MOVIMIENTO_REGISTRADO } })
  })

  it('una referencia inválida marca el campo de referencia', async () => {
    registrarMovimiento.mockRejectedValue(
      new ErrorHttp(400, {
        codigo: 'referencia_invalida',
        mensaje: 'La factura elegida ya no está pendiente de cobro.',
        campo: 'facturaId',
      }),
    )
    const usuario = userEvent.setup()
    renderizar()

    await completar(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Guardar movimiento' }))

    expect(await screen.findByText('La factura elegida ya no está pendiente de cobro.')).toBeInTheDocument()
    expect(screen.getByLabelText('Referencia')).toHaveAttribute('aria-invalid', 'true')
  })

  /** CA3: sin caja operable no hay formulario. */
  it('una caja cerrada muestra el aviso y ningún campo', async () => {
    obtenerCaja.mockResolvedValue(caja({ estado: 'cerrada', puedeOperar: false }))
    renderizar()

    const aviso = await screen.findByRole('alert')
    expect(aviso).toHaveTextContent(MENSAJE_SIN_CAJA_ABIERTA)
    expect(within(aviso).getByRole('link', { name: 'Ir a cajas' })).toHaveAttribute('href', '/caja')
    expect(screen.queryByLabelText('Importe')).not.toBeInTheDocument()
  })

  it('una caja inexistente muestra el mismo aviso', async () => {
    obtenerCaja.mockRejectedValue(new ErrorHttp(404, { codigo: 'caja_no_encontrada', mensaje: 'No encontramos esa caja.' }))
    renderizar()

    expect(await screen.findByRole('alert')).toHaveTextContent(MENSAJE_SIN_CAJA_ABIERTA)
  })

  it('un 409 caja_cerrada al guardar muestra el aviso y conserva lo cargado', async () => {
    registrarMovimiento.mockRejectedValue(
      new ErrorHttp(409, {
        codigo: 'caja_cerrada',
        mensaje: 'Esta caja ya está cerrada y no admite nuevos movimientos.',
      }),
    )
    const usuario = userEvent.setup()
    renderizar()

    await completar(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Guardar movimiento' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(MENSAJE_SIN_CAJA_ABIERTA)
    expect(screen.getByLabelText('Importe')).toHaveValue('3.500')
    expect(screen.getByLabelText('Concepto')).toHaveValue('Cobro flete')
    expect(navegar).not.toHaveBeenCalled()
  })

  it('un 403 al guardar muestra el texto de acción sin permiso', async () => {
    registrarMovimiento.mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso.' }))
    const usuario = userEvent.setup()
    renderizar()

    await completar(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Guardar movimiento' }))

    expect(await screen.findByRole('alert')).toHaveTextContent(MENSAJE_ACCION_SIN_PERMISO)
  })

  it('sin permiso muestra el aviso sin cargar la caja', () => {
    renderizar(false)

    expect(screen.getByRole('alert')).toBeInTheDocument()
    expect(screen.queryByLabelText('Importe')).not.toBeInTheDocument()
    expect(obtenerCaja).not.toHaveBeenCalled()
  })
})
