import { render, screen, waitFor, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ListadoLiquidaciones } from './ListadoLiquidaciones'
import type { LiquidacionListado, PaginaDe } from '../servicios/servicioLiquidaciones'

const listarLiquidaciones = vi.fn()
const listarTransportistas = vi.fn()

vi.mock('../servicios/servicioLiquidaciones', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioLiquidaciones')>(
    '../servicios/servicioLiquidaciones',
  )

  return {
    ...real,
    listarLiquidaciones: (...args: unknown[]) => listarLiquidaciones(...args),
    listarTransportistas: (...args: unknown[]) => listarTransportistas(...args),
  }
})

function liquidacion(parcial: Partial<LiquidacionListado>): LiquidacionListado {
  return {
    id: 1,
    numero: 'LQ-12',
    mes: 7,
    anio: 2026,
    transportista: { id: 3, razonSocial: 'Transportes Díaz', cuit: '20123456786', activo: true },
    importeTotal: 355_000,
    restaPagar: 155_000,
    estado: 'pendiente',
    motivoAnulacion: null,
    ...parcial,
  }
}

function pagina(items: LiquidacionListado[]): PaginaDe<LiquidacionListado> {
  return { items, total: items.length, pagina: 1, tamanioPagina: 20 }
}

function renderizar(puedeGestionar = true) {
  render(
    <MemoryRouter>
      <ListadoLiquidaciones puedeGestionar={puedeGestionar} />
    </MemoryRouter>,
  )
}

describe('ListadoLiquidaciones', () => {
  beforeEach(() => {
    listarTransportistas.mockReset().mockResolvedValue({
      empresaEmisoraConfigurada: true,
      transportistas: [],
    })
    listarLiquidaciones.mockReset().mockResolvedValue(
      pagina([
        liquidacion({ id: 1, numero: 'LQ-12' }),
        liquidacion({ id: 2, numero: 'LQ-11', estado: 'pagada', restaPagar: 0 }),
        liquidacion({
          id: 3,
          numero: 'LQ-10',
          estado: 'anulada',
          restaPagar: null,
          motivoAnulacion: 'El período no correspondía.',
          transportista: { id: 4, razonSocial: 'Fletes del Sur S.R.L.', cuit: '30709876542', activo: false },
        }),
      ]),
    )
  })

  /** FR-021, FR-027: cada fila con su período, su CUIT con guiones y lo que resta pagar. */
  it('muestra cada fila con su resta pagar según el estado', async () => {
    renderizar()

    const tabla = await screen.findByRole('table')
    const filas = within(tabla).getAllByRole('row')

    const pendiente = filas.find((fila) => within(fila).queryByText('LQ-12'))!
    expect(within(pendiente).getByText('07/2026')).toBeInTheDocument()
    expect(within(pendiente).getByText('20-12345678-6')).toBeInTheDocument()
    expect(within(pendiente).getByText('$ 355.000,00')).toBeInTheDocument()
    expect(within(pendiente).getByText('$ 155.000,00')).toBeInTheDocument()

    const pagada = filas.find((fila) => within(fila).queryByText('LQ-11'))!
    expect(within(pagada).getByText('$ 0,00')).toBeInTheDocument()
  })

  /** FR-032: la anulada va atenuada y con la palabra de su estado, su motivo y sin importe por pagar. */
  it('atenúa la anulada y la explica con palabras', async () => {
    renderizar()

    const tabla = await screen.findByRole('table')
    const anulada = within(tabla).getAllByRole('row').find((fila) => within(fila).queryByText('LQ-10'))!

    expect(anulada).toHaveClass('atenuada')
    expect(within(anulada).getByText('Anulada')).toBeInTheDocument()
    expect(within(anulada).getByText('El período no correspondía.')).toBeInTheDocument()
    expect(within(anulada).getByText('No corresponde')).toBeInTheDocument()
    expect(within(anulada).getByText('Inactivo')).toBeInTheDocument()
  })

  it('declara qué está mostrando con y sin filtro de estado', async () => {
    const usuario = userEvent.setup()
    renderizar()

    expect(
      await screen.findByText('Mostrando todas las liquidaciones, incluidas las anuladas.'),
    ).toHaveAttribute('role', 'status')

    await usuario.selectOptions(screen.getByLabelText('Estado'), 'pendiente')

    expect(await screen.findByText('Mostrando sólo las liquidaciones pendientes.')).toBeInTheDocument()
    await waitFor(() =>
      expect(listarLiquidaciones).toHaveBeenLastCalledWith(
        expect.objectContaining({ estado: 'pendiente' }),
        1,
      ),
    )
  })

  it('distingue la lista vacía de la que no coincide con los filtros', async () => {
    listarLiquidaciones.mockResolvedValue(pagina([]))
    const usuario = userEvent.setup()
    renderizar()

    expect(
      await screen.findByText(
        'Todavía no se generó ninguna liquidación. Generá la primera eligiendo un transportista y un período.',
      ),
    ).toBeInTheDocument()

    await usuario.selectOptions(screen.getByLabelText('Año'), '2025')

    expect(
      await screen.findByText('Ninguna liquidación coincide con los filtros aplicados. Probá limpiarlos.'),
    ).toBeInTheDocument()
  })

  /** FR-065: quien sólo consulta no ve el botón de generar. */
  it('sin permiso de gestión no ofrece generar', async () => {
    renderizar(false)

    await screen.findByRole('table')

    expect(screen.queryByRole('link', { name: 'Generar liquidación' })).not.toBeInTheDocument()
  })

  it('con permiso de gestión ofrece generar', async () => {
    renderizar()

    expect(await screen.findByRole('link', { name: 'Generar liquidación' })).toHaveAttribute(
      'href',
      '/liquidaciones/nueva',
    )
  })
})
