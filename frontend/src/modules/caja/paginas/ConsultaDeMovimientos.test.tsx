import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { ConsultaDeMovimientos, MENSAJE_SIN_MOVIMIENTOS } from './ConsultaDeMovimientos'
import {
  MENSAJE_SIN_PERMISO,
  type MovimientoListado,
  type PaginaDeMovimientos,
} from '../servicios/servicioCaja'

const listarMovimientos = vi.fn()
const listarCajas = vi.fn()

vi.mock('../servicios/servicioCaja', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioCaja')>('../servicios/servicioCaja')

  return {
    ...real,
    listarMovimientos: (...args: unknown[]) => listarMovimientos(...args),
    listarCajas: (...args: unknown[]) => listarCajas(...args),
  }
})

const CON_REFERENCIA: MovimientoListado = {
  id: 1,
  cajaId: 12,
  fecha: '2026-09-16T15:00:00Z',
  tipo: 'egreso',
  importe: 2_000,
  concepto: 'Pago de peaje',
  responsable: { id: 3, nombre: 'admin.empresa' },
  referencia: 'OP-4',
}

const SIN_REFERENCIA: MovimientoListado = { ...CON_REFERENCIA, id: 2, tipo: 'ingreso', concepto: 'Cobro', referencia: null }

function pagina(items: MovimientoListado[]): PaginaDeMovimientos {
  return { items, total: items.length, pagina: 1, tamanioPagina: 20 }
}

function renderizar(ruta = '/movimientos-caja', puedeEmitirReportes = true) {
  render(
    <MemoryRouter initialEntries={[ruta]}>
      <ConsultaDeMovimientos puedeEmitirReportes={puedeEmitirReportes} />
    </MemoryRouter>,
  )
}

describe('ConsultaDeMovimientos', () => {
  beforeEach(() => {
    listarMovimientos.mockReset().mockResolvedValue(pagina([CON_REFERENCIA, SIN_REFERENCIA]))
    listarCajas.mockReset().mockResolvedValue({
      items: [
        {
          id: 12,
          responsable: { id: 3, nombre: 'admin.empresa' },
          fechaApertura: '2026-09-16T11:00:00Z',
          estado: 'cerrada',
          fechaCierre: '2026-09-16T21:00:00Z',
          saldoFinal: 8_000,
        },
      ],
      total: 1,
      pagina: 1,
      tamanioPagina: 20,
    })
  })

  /** CA5. */
  it('muestra las seis columnas, con y sin referencia', async () => {
    renderizar()

    const tabla = await screen.findByRole('table', { name: 'Movimientos de caja' })
    const [, primera, segunda] = within(tabla).getAllByRole('row')

    expect(within(primera).getByText('Egreso')).toBeInTheDocument()
    expect(within(primera).getByText('$ 2.000,00')).toBeInTheDocument()
    expect(within(primera).getByText('Pago de peaje')).toBeInTheDocument()
    expect(within(primera).getByText('admin.empresa')).toBeInTheDocument()
    expect(within(primera).getByText('OP-4')).toBeInTheDocument()

    expect(within(segunda).getByText('Ingreso')).toBeInTheDocument()
    expect(within(segunda).queryByText('OP-4')).not.toBeInTheDocument()
  })

  /** FR-027: el listado declara qué está mostrando. */
  it('declara el filtro aplicado', async () => {
    const usuario = userEvent.setup()
    renderizar()

    expect(await screen.findByText('Mostrando los movimientos de todas las cajas.')).toBeInTheDocument()

    await usuario.type(screen.getByLabelText('Desde'), '2026-09-15')

    expect(await screen.findByText('Mostrando los movimientos desde el 15/09/2026.')).toBeInTheDocument()
    expect(listarMovimientos).toHaveBeenLastCalledWith({ desde: '2026-09-15', hasta: '', cajaId: '' }, 1)
  })

  /** US4 esc. 3. */
  it('el rango invertido marca los campos y no consulta', async () => {
    const usuario = userEvent.setup()
    renderizar()
    await screen.findByRole('table')

    await usuario.type(screen.getByLabelText('Desde'), '2026-09-16')
    const llamadas = listarMovimientos.mock.calls.length
    await usuario.type(screen.getByLabelText('Hasta'), '2026-09-15')

    expect(screen.getByText('La fecha desde es posterior a la hasta.')).toBeInTheDocument()
    expect(screen.getByLabelText('Desde')).toHaveAttribute('aria-invalid', 'true')
    expect(screen.getByLabelText('Hasta')).toHaveAttribute('aria-invalid', 'true')
    expect(listarMovimientos.mock.calls.length).toBe(llamadas)
  })

  /** CA9, CL2, FR-025. */
  it('un período sin movimientos muestra el mensaje y no una tabla vacía', async () => {
    listarMovimientos.mockResolvedValue(pagina([]))
    renderizar()

    expect(await screen.findByText(MENSAJE_SIN_MOVIMIENTOS)).toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('con cajaId en la dirección llega filtrada', async () => {
    renderizar('/movimientos-caja?cajaId=12')

    await screen.findByRole('table')
    expect(listarMovimientos).toHaveBeenCalledWith({ desde: '', hasta: '', cajaId: 12 }, 1)
    expect(screen.getByText('Mostrando los movimientos de la caja elegida.')).toBeInTheDocument()
  })

  it('no ofrece ninguna acción de escritura', async () => {
    renderizar()

    await screen.findByRole('table')
    expect(screen.queryByRole('link', { name: /Abrir caja|Registrar movimiento|Cerrar caja/ })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: /Abrir caja|Registrar movimiento|Cerrar caja/ })).not.toBeInTheDocument()
  })

  it('un 403 al cargar muestra el aviso sin permiso y no el error de carga', async () => {
    listarMovimientos.mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso.' }))
    renderizar()

    expect(await screen.findByRole('alert')).toHaveTextContent(MENSAJE_SIN_PERMISO)
    expect(screen.queryByText(/Volvé a intentar/)).not.toBeInTheDocument()
  })

  // ── Módulo 12: la acción de reporte (US3) ───────────────────────────────────────────────────

  it('con el permiso ofrece Generar reporte, y la pantalla sigue sin acción primaria', async () => {
    renderizar()

    expect(await screen.findByRole('button', { name: 'Generar reporte' })).toBeEnabled()
    expect(screen.queryByRole('button', { name: /Registrar movimiento/ })).toBeNull()
  })

  it('sin el permiso la acción no aparece', async () => {
    renderizar('/movimientos-caja', false)

    await screen.findByRole('table')

    expect(screen.queryByRole('button', { name: 'Generar reporte' })).toBeNull()
  })

  /** Con un rango que no deja ningún movimiento, la acción se deshabilita y se explica (FR-003). */
  it('sin movimientos para el filtro la acción queda deshabilitada y se explica por qué', async () => {
    listarMovimientos.mockResolvedValue(pagina([]))

    renderizar()

    expect(await screen.findByRole('button', { name: 'Generar reporte' })).toBeDisabled()
    expect(screen.getByText('No hay filas para reportar.')).toBeVisible()
  })
})
