import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ListadoViajes } from './ListadoViajes'
import type { PaginaDe } from '../clientes/servicioClientes'
import type { ViajeListado } from '../servicios/servicioViajes'

const listarViajes = vi.fn()
const listarClientes = vi.fn()
const listarTransportistas = vi.fn()

vi.mock('../servicios/servicioViajes', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioViajes')>(
    '../servicios/servicioViajes',
  )
  return { ...real, listarViajes: (...args: unknown[]) => listarViajes(...args) }
})

vi.mock('../clientes/servicioClientes', async () => {
  const real = await vi.importActual<typeof import('../clientes/servicioClientes')>(
    '../clientes/servicioClientes',
  )
  return { ...real, listarClientes: (...args: unknown[]) => listarClientes(...args) }
})

vi.mock('../../choferes/transportistas/servicioTransportistas', async () => {
  const real = await vi.importActual<
    typeof import('../../choferes/transportistas/servicioTransportistas')
  >('../../choferes/transportistas/servicioTransportistas')
  return { ...real, listarTransportistas: (...args: unknown[]) => listarTransportistas(...args) }
})

function viaje(parcial: Partial<ViajeListado> = {}): ViajeListado {
  return {
    id: 1,
    numero: 1041,
    fecha: '2026-08-10',
    cliente: { id: 1, nombre: 'Distribuidora del Litoral', activo: true },
    origen: 'Rosario',
    destino: 'Córdoba',
    chofer: { id: 3, nombre: 'Gómez, Juan', activo: true },
    vehiculo: { id: 5, nombre: 'AB123CD', activo: true },
    transportista: { id: 2, nombre: 'Transporte Sur', activo: true },
    estado: 'pendiente',
    importe: 1_240_000,
    demorado: false,
    esRetroactivo: false,
    motivoAnulacion: null,
    factura: null,
    ...parcial,
  }
}

function pagina(items: ViajeListado[], total = items.length, numero = 1): PaginaDe<ViajeListado> {
  return { items, total, pagina: numero, tamanioPagina: 20 }
}

function renderizar(puedeGestionar = true) {
  return render(
    <MemoryRouter>
      <ListadoViajes puedeGestionar={puedeGestionar} />
    </MemoryRouter>,
  )
}

describe('ListadoViajes', () => {
  beforeEach(() => {
    listarViajes.mockReset()
    listarViajes.mockResolvedValue(pagina([viaje()]))
    listarClientes.mockReset()
    listarClientes.mockResolvedValue(pagina([]))
    listarTransportistas.mockReset()
    listarTransportistas.mockResolvedValue([])
  })

  it('avisa que todavía no hay viajes registrados', async () => {
    listarViajes.mockResolvedValue(pagina([]))

    renderizar()

    expect(
      await screen.findByText('Todavía no hay viajes registrados. Registrá el primero para empezar.'),
    ).toBeInTheDocument()

    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('distingue "no hay ninguno" de "los filtros no encontraron nada"', async () => {
    listarViajes.mockResolvedValue(pagina([]))

    const usuario = userEvent.setup()
    renderizar()

    await screen.findByText('Todavía no hay viajes registrados. Registrá el primero para empezar.')

    await usuario.type(screen.getByLabelText('Buscar por origen, destino o cliente'), 'ushuaia')

    expect(
      await screen.findByText('Ningún viaje coincide con los filtros aplicados.'),
    ).toBeInTheDocument()
  })

  /**
   * FR-044 y FR-049: **el control siempre dice qué está mostrando**. Sin filtro de estado los
   * anulados no aparecen, y callárselo haría leer el listado como un error de datos.
   */
  it('el control de estado dice que está ocultando los anulados', async () => {
    renderizar()

    await screen.findByRole('table')

    expect(
      screen.getByText('Mostrando todos los viajes menos los anulados. Elegí "Anulado" para verlos.'),
    ).toBeInTheDocument()

    // Y la opción por defecto se llama con todas las letras.
    expect(
      within(screen.getByLabelText('Estado')).getByRole('option', {
        name: 'Todos menos anulados',
      }),
    ).toBeInTheDocument()
  })

  it('el control dice qué estado está filtrando al elegir uno', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await screen.findByRole('table')

    await usuario.selectOptions(screen.getByLabelText('Estado'), 'anulado')

    expect(await screen.findByText('Mostrando sólo: Anulado.')).toBeInTheDocument()
  })

  /** FR-039: `Demorado` acompaña al estado con una palabra, no con un color. */
  it('muestra la etiqueta Demorado junto al estado En curso', async () => {
    listarViajes.mockResolvedValue(pagina([viaje({ estado: 'enCurso', demorado: true })]))

    renderizar()

    const tabla = await screen.findByRole('table')

    expect(within(tabla).getByText('En curso')).toBeInTheDocument()
    expect(within(tabla).getByText('Demorado')).toBeInTheDocument()
  })

  /** FR-016: la carga retroactiva también se dice con palabras, junto a la fecha. */
  it('muestra la etiqueta Carga retroactiva junto a la fecha', async () => {
    listarViajes.mockResolvedValue(pagina([viaje({ esRetroactivo: true })]))

    renderizar()

    const tabla = await screen.findByRole('table')

    expect(within(tabla).getByText('Carga retroactiva')).toBeInTheDocument()
  })

  /** FR-008 y FR-030: el cliente, el chofer y el vehículo dados de baja se siguen mostrando. */
  it('acompaña a las unidades inactivas con la palabra (inactivo)', async () => {
    listarViajes.mockResolvedValue(
      pagina([
        viaje({
          cliente: { id: 1, nombre: 'Distribuidora del Litoral', activo: false },
          chofer: { id: 3, nombre: 'Gómez, Juan', activo: false },
          vehiculo: { id: 5, nombre: 'AB123CD', activo: false },
        }),
      ]),
    )

    renderizar()

    const tabla = await screen.findByRole('table')

    expect(within(tabla).getByText('Distribuidora del Litoral (inactivo)')).toBeInTheDocument()
    expect(within(tabla).getByText('Gómez, Juan (inactivo)')).toBeInTheDocument()
    expect(within(tabla).getByText('AB123CD (inactivo)')).toBeInTheDocument()
  })

  /**
   * FR-046a: **la tabla no tiene fila de total**. Los totales viven en su pantalla, y sumar la
   * página en curso daría un número que no es el del período.
   */
  it('la tabla no tiene fila de total de importes', async () => {
    listarViajes.mockResolvedValue(
      pagina([viaje({ id: 1, importe: 1_000_000 }), viaje({ id: 2, importe: 240_000 })]),
    )

    const { container } = renderizar()

    await screen.findByRole('table')

    expect(container.querySelector('tfoot')).toBeNull()
    expect(screen.queryByText('$ 1.240.000,00')).not.toBeInTheDocument()
  })

  it('formatea los importes en pesos argentinos', async () => {
    renderizar()

    const tabla = await screen.findByRole('table')

    expect(within(tabla).getByText('$ 1.240.000,00')).toBeInTheDocument()
  })

  it('vuelve a la página 1 al cambiar cualquier filtro', async () => {
    listarViajes.mockResolvedValue(pagina([viaje()], 73, 3))

    const usuario = userEvent.setup()
    renderizar()

    await screen.findByRole('table')
    listarViajes.mockClear()

    await usuario.selectOptions(screen.getByLabelText('Estado'), 'rendido')

    expect(listarViajes).toHaveBeenLastCalledWith(
      expect.objectContaining({ estado: 'rendido' }),
      1,
    )
  })

  it('anuncia el cambio de página con role="status"', async () => {
    listarViajes.mockResolvedValue(pagina([viaje()], 73, 2))

    renderizar()

    const paginacion = await screen.findByRole('navigation', { name: 'Paginación' })

    expect(within(paginacion).getByRole('status')).toHaveTextContent(
      'Página 2 de 4, mostrando 21 a 40 de 73 viajes',
    )
  })

  /** FR-052: quien sólo consulta no ve el botón de alta. */
  it('sin permiso de gestión no ofrece el alta', async () => {
    renderizar(false)

    await screen.findByRole('table')

    expect(screen.queryByRole('link', { name: 'Nuevo viaje' })).not.toBeInTheDocument()
  })
})
