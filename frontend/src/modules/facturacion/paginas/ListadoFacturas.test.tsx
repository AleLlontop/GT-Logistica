import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ListadoFacturas } from './ListadoFacturas'
import type { FacturaListado } from '../servicios/servicioFacturas'

const listarFacturas = vi.fn()

vi.mock('../servicios/servicioFacturas', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioFacturas')>(
    '../servicios/servicioFacturas',
  )

  return { ...real, listarFacturas: (...args: unknown[]) => listarFacturas(...args) }
})

vi.mock('../../viajes/clientes/servicioClientes', async () => {
  const real = await vi.importActual<typeof import('../../viajes/clientes/servicioClientes')>(
    '../../viajes/clientes/servicioClientes',
  )

  return {
    ...real,
    listarClientes: () => Promise.resolve({ items: [], total: 0, pagina: 1, tamanioPagina: 20 }),
  }
})

function factura(parcial: Partial<FacturaListado> & { id: number }): FacturaListado {
  return {
    numeroComprobante: `0014-0000000${parcial.id}`,
    fecha: '2026-08-12',
    cliente: { id: 1, razonSocial: 'Distribuidora del Litoral', activo: true },
    tipoComprobante: 'facturaA',
    mes: 8,
    anio: 2026,
    total: 121_000,
    estado: 'pendiente',
    vencimientoPago: '2026-09-11',
    motivoAnulacion: null,
    fechaCobro: null,
    ...parcial,
  }
}

function pagina(items: FacturaListado[], total = items.length) {
  return { items, total, pagina: 1, tamanioPagina: 20 }
}

function renderizar(puedeGestionar = true) {
  render(
    <MemoryRouter>
      <ListadoFacturas puedeGestionar={puedeGestionar} />
    </MemoryRouter>,
  )
}

describe('ListadoFacturas', () => {
  beforeEach(() => {
    listarFacturas.mockReset().mockResolvedValue(pagina([factura({ id: 1 })]))
  })

  /**
   * FR-064: el control dice qué está mostrando, y **sin filtro sí incluye las anuladas** — al revés que
   * el listado de viajes. Una factura anulada sigue siendo parte de la historia de cobranza.
   */
  it('el control dice que está mostrando todas, incluidas las anuladas', async () => {
    renderizar()

    expect(
      await screen.findByText('Mostrando todas las facturas, incluidas las anuladas.'),
    ).toBeInTheDocument()
  })

  it('el control dice qué estado está filtrando al elegir uno', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.selectOptions(await screen.findByLabelText('Estado'), 'vencida')

    expect(
      await screen.findByText('Mostrando sólo las facturas vencidas.'),
    ).toBeInTheDocument()
  })

  /** FR-065: el estado va con la palabra y nunca sólo con un color. */
  it('muestra cada estado con su palabra', async () => {
    listarFacturas.mockResolvedValue(
      pagina([
        factura({ id: 1, estado: 'pendiente' }),
        factura({ id: 2, estado: 'vencida', vencimientoPago: '2026-01-01' }),
        factura({ id: 3, estado: 'pagada', fechaCobro: '2026-09-01' }),
        factura({ id: 4, estado: 'anulada', motivoAnulacion: 'Cliente equivocado.' }),
      ]),
    )

    renderizar()

    // Dentro de la tabla: los mismos nombres viven también en el desplegable de estado, y lo que este
    // test verifica es que **la fila** lleve la palabra (FR-065).
    const tabla = await screen.findByRole('table')

    expect(within(tabla).getByText(/^Pendiente/)).toBeInTheDocument()
    expect(within(tabla).getByText(/^Vencida/)).toBeInTheDocument()
    expect(within(tabla).getByText(/^Pagada/)).toBeInTheDocument()
    expect(within(tabla).getByText(/^Anulada/)).toBeInTheDocument()
  })

  /**
   * FR-065: la fila anulada va atenuada **y** con el motivo visible. El color no alcanza, y el motivo es
   * lo que explica por qué esa fila no cuenta.
   */
  it('la fila anulada va atenuada y con su motivo a la vista', async () => {
    listarFacturas.mockResolvedValue(
      pagina([factura({ id: 4, estado: 'anulada', motivoAnulacion: 'Cliente equivocado.' })]),
    )

    renderizar()

    expect(await screen.findByText(/Cliente equivocado\./)).toBeInTheDocument()

    const fila = screen.getByText(/Cliente equivocado\./).closest('tr')
    expect(fila).toHaveClass('atenuada')
  })

  /** La pagada dice cuándo se cobró: el estado solo no lo cuenta (contracts/README §Listado). */
  it('la fila pagada muestra su fecha de cobro', async () => {
    listarFacturas.mockResolvedValue(
      pagina([factura({ id: 3, estado: 'pagada', fechaCobro: '2026-09-01' })]),
    )

    renderizar()

    expect(await screen.findByText(/Cobrada el 01\/09\/2026/)).toBeInTheDocument()
  })

  /**
   * FR-011 y US3 esc. 9: un cliente dado de baja después de facturado se muestra con su razón social
   * **congelada** y la palabra `Inactivo` al lado.
   */
  it('señala el cliente inactivo con la palabra', async () => {
    listarFacturas.mockResolvedValue(
      pagina([
        factura({
          id: 1,
          cliente: { id: 1, razonSocial: 'Distribuidora del Litoral', activo: false },
        }),
      ]),
    )

    renderizar()

    expect(
      await screen.findByText('Distribuidora del Litoral (Inactivo)'),
    ).toBeInTheDocument()
  })

  /** Los importes con `formatearPesos`, nunca con `toFixed(2)` (convención [005]). */
  it('formatea los importes en pesos argentinos', async () => {
    listarFacturas.mockResolvedValue(pagina([factura({ id: 1, total: 1_240_000 })]))

    renderizar()

    expect(await screen.findByText('$ 1.240.000,00')).toBeInTheDocument()
  })

  /** Las fechas con `formatearFecha`: `new Date(iso)` mostraría el día anterior en UTC−3. */
  it('formatea las fechas en dd/MM/yyyy', async () => {
    renderizar()

    expect(await screen.findByText('12/08/2026')).toBeInTheDocument()
    expect(screen.getByText('11/09/2026')).toBeInTheDocument()
  })

  /** Dos mensajes distintos: "no emitiste ninguna" y "el filtro no encontró nada". */
  it('sin facturas invita a emitir la primera', async () => {
    listarFacturas.mockResolvedValue(pagina([]))

    renderizar()

    expect(
      await screen.findByText(
        'Todavía no se emitió ninguna factura. Emití la primera para empezar a seguir la cobranza.',
      ),
    ).toBeInTheDocument()
  })

  it('con filtros y sin coincidencias lo dice distinto', async () => {
    listarFacturas.mockResolvedValue(pagina([]))

    const usuario = userEvent.setup()
    renderizar()

    await usuario.selectOptions(await screen.findByLabelText('Estado'), 'pagada')

    expect(
      await screen.findByText('Ninguna factura coincide con los filtros aplicados.'),
    ).toBeInTheDocument()
  })

  /** Cualquier cambio de filtro vuelve a la página 1. */
  it('vuelve a la primera página al cambiar un filtro', async () => {
    listarFacturas.mockResolvedValue(pagina([factura({ id: 1 })], 45))

    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Siguiente' }))
    await usuario.selectOptions(screen.getByLabelText('Estado'), 'vencida')

    const ultimaLlamada = listarFacturas.mock.calls.at(-1)!
    expect(ultimaLlamada[1]).toBe(1)
  })

  /** Quien sólo consulta no ve el botón de emitir (FR-068). */
  it('sin permiso de gestión no ofrece el alta', async () => {
    renderizar(false)

    await screen.findByText('Mostrando todas las facturas, incluidas las anuladas.')

    expect(screen.queryByRole('link', { name: 'Nueva factura' })).not.toBeInTheDocument()
  })

  it('con permiso de gestión ofrece el alta', async () => {
    renderizar(true)

    expect(await screen.findByRole('link', { name: 'Nueva factura' })).toBeInTheDocument()
  })
})
