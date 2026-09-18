import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { formatearInstante } from '../../../compartido/fechas'
import { DetalleDeCaja } from './DetalleDeCaja'
import {
  MENSAJE_SIN_PERMISO,
  type CajaDetalle,
  type MovimientoListado,
  type PaginaDeMovimientos,
} from '../servicios/servicioCaja'

vi.mock('react-router-dom', async () => {
  const real = await vi.importActual<typeof import('react-router-dom')>('react-router-dom')

  return { ...real, useParams: () => ({ id: '12' }) }
})

const obtenerCaja = vi.fn()
const listarMovimientosDeCaja = vi.fn()

vi.mock('../servicios/servicioCaja', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioCaja')>('../servicios/servicioCaja')

  return {
    ...real,
    obtenerCaja: (...args: unknown[]) => obtenerCaja(...args),
    listarMovimientosDeCaja: (...args: unknown[]) => listarMovimientosDeCaja(...args),
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
    totalIngresos: 3_500,
    totalEgresos: 2_000,
    saldoActual: 11_500,
    puedeOperar: true,
    ...parcial,
  }
}

const INGRESO: MovimientoListado = {
  id: 1,
  cajaId: 12,
  fecha: '2026-09-18T12:00:00Z',
  tipo: 'ingreso',
  importe: 3_500,
  concepto: 'Cobro flete',
  responsable: { id: 3, nombre: 'admin.empresa' },
  referencia: 'Factura 0014-00000003 · Distribuidora del Litoral',
}

function pagina(items: MovimientoListado[]): PaginaDeMovimientos {
  return { items, total: items.length, pagina: 1, tamanioPagina: 20 }
}

function renderizar(puedeGestionar = true, aviso?: string) {
  render(
    <MemoryRouter initialEntries={[{ pathname: '/caja/12', state: aviso ? { aviso } : null }]}>
      <DetalleDeCaja puedeGestionar={puedeGestionar} />
    </MemoryRouter>,
  )
}

describe('DetalleDeCaja', () => {
  beforeEach(() => {
    obtenerCaja.mockReset().mockResolvedValue(caja())
    listarMovimientosDeCaja.mockReset().mockResolvedValue(pagina([INGRESO]))
  })

  /** SC-007: cuánto entró y cuánto salió sin sumar a mano. */
  it('muestra los datos, los totales y el saldo actual', async () => {
    renderizar()

    expect(await screen.findByText(`admin.empresa · abierta el ${formatearInstante('2026-09-18T11:00:00Z')}`)).toBeInTheDocument()

    const resumen = screen.getByRole('region', { name: 'Resumen' })
    expect(within(resumen).getByText('$ 10.000,00')).toBeInTheDocument()
    expect(within(resumen).getByText('$ 3.500,00')).toBeInTheDocument()
    expect(within(resumen).getByText('$ 2.000,00')).toBeInTheDocument()
    expect(within(resumen).getByText('$ 11.500,00')).toBeInTheDocument()
    expect(screen.getByText('Abierta')).toBeInTheDocument()
  })

  it('una cerrada muestra cierre y saldo final, sin acciones', async () => {
    obtenerCaja.mockResolvedValue(
      caja({ estado: 'cerrada', fechaCierre: '2026-09-18T21:00:00Z', saldoFinal: 11_500, puedeOperar: false }),
    )
    renderizar()

    expect(await screen.findByText(`admin.empresa · abierta el ${formatearInstante('2026-09-18T11:00:00Z')} · cerrada el ${formatearInstante('2026-09-18T21:00:00Z')}`)).toBeInTheDocument()
    expect(within(screen.getByRole('region', { name: 'Resumen' })).getByText('Saldo final')).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Registrar movimiento' })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Cerrar caja' })).not.toBeInTheDocument()
    expect(screen.getByRole('link', { name: 'Volver a cajas' })).toBeInTheDocument()
  })

  /** Convención [008]: el anuncio va en el `<p>` que contiene el texto. */
  it('anuncia el mensaje recibido por navegación', async () => {
    renderizar(true, 'Caja abierta con éxito.')

    const anuncio = await screen.findByRole('status')
    expect(anuncio.tagName).toBe('P')
    expect(anuncio).toHaveTextContent('Caja abierta con éxito.')
  })

  it('un 403 al cargar muestra el aviso sin permiso', async () => {
    obtenerCaja.mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso.' }))
    renderizar(false)

    expect(await screen.findByRole('alert')).toHaveTextContent(MENSAJE_SIN_PERMISO)
    expect(screen.queryByText(/Volvé a intentar/)).not.toBeInTheDocument()
  })

  // ── User Story 2 ──────────────────────────────────────────────────────────────────────────────

  /** CA5: las seis columnas, con la referencia. */
  it('la tabla muestra las seis columnas con la referencia', async () => {
    renderizar()

    const tabla = await screen.findByRole('table', { name: 'Movimientos de la caja' })
    const encabezados = within(tabla).getAllByRole('columnheader').map((celda) => celda.textContent)
    expect(encabezados).toEqual(['Fecha', 'Tipo', 'Importe', 'Concepto', 'Responsable', 'Referencia'])

    const [, fila] = within(tabla).getAllByRole('row')
    expect(within(fila).getByText(formatearInstante(INGRESO.fecha))).toBeInTheDocument()
    expect(within(fila).getByText('Ingreso')).toBeInTheDocument()
    expect(within(fila).getByText('$ 3.500,00')).toBeInTheDocument()
    expect(within(fila).getByText('Cobro flete')).toBeInTheDocument()
    expect(within(fila).getByText('admin.empresa')).toBeInTheDocument()
    expect(within(fila).getByText('Factura 0014-00000003 · Distribuidora del Litoral')).toBeInTheDocument()
  })

  it('sin movimientos lo dice en lugar de la tabla', async () => {
    listarMovimientosDeCaja.mockResolvedValue(pagina([]))
    renderizar()

    expect(await screen.findByText('Esta caja no tiene movimientos.')).toBeInTheDocument()
    expect(screen.queryByRole('table', { name: 'Movimientos de la caja' })).not.toBeInTheDocument()
  })

  it('Registrar movimiento sólo con el permiso y una caja operable', async () => {
    renderizar()

    expect(await screen.findByRole('link', { name: 'Registrar movimiento' })).toHaveAttribute(
      'href',
      '/caja/12/movimientos/nuevo',
    )
  })

  it('una caja de otro empleado se ve sin acciones', async () => {
    obtenerCaja.mockResolvedValue(caja({ puedeOperar: false }))
    renderizar()

    await screen.findByRole('region', { name: 'Resumen' })
    expect(screen.queryByRole('link', { name: 'Registrar movimiento' })).not.toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Cerrar caja' })).not.toBeInTheDocument()
  })

  it('sin permiso de gestión, ni la propia se opera', async () => {
    renderizar(false)

    await screen.findByRole('region', { name: 'Resumen' })
    expect(screen.queryByRole('link', { name: 'Registrar movimiento' })).not.toBeInTheDocument()
  })

  // ── User Story 3 ──────────────────────────────────────────────────────────────────────────────

  /** Cerrar caja es secundaria: la primaria es Registrar movimiento (plan §UI Design Check). */
  it('Cerrar caja es secundaria y lleva al resumen', async () => {
    renderizar()

    const cerrar = await screen.findByRole('link', { name: 'Cerrar caja' })
    expect(cerrar).toHaveAttribute('href', '/caja/12/cierre')
    expect(cerrar.className).not.toBe(screen.getByRole('link', { name: 'Registrar movimiento' }).className)
  })

  it('una caja cerrada no ofrece cerrarla', async () => {
    obtenerCaja.mockResolvedValue(caja({ estado: 'cerrada', fechaCierre: '2026-09-18T21:00:00Z', saldoFinal: 1, puedeOperar: false }))
    renderizar()

    await screen.findByRole('region', { name: 'Resumen' })
    expect(screen.queryByRole('link', { name: 'Cerrar caja' })).not.toBeInTheDocument()
  })

  // ── User Story 4 ──────────────────────────────────────────────────────────────────────────────

  it('ofrece ver sus movimientos en la consulta, ya filtrada', async () => {
    renderizar()

    expect(await screen.findByRole('link', { name: 'Ver en movimientos de caja' })).toHaveAttribute(
      'href',
      '/movimientos-caja?cajaId=12',
    )
  })
})
