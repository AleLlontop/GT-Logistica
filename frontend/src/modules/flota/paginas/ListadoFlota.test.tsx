import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ListadoFlota } from './ListadoFlota'
import type { PaginaDe, VehiculoListado } from '../servicios/servicioFlota'

const listarFlota = vi.fn()
const listarTransportistas = vi.fn()
const listarTiposVehiculo = vi.fn()

vi.mock('../servicios/servicioFlota', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioFlota')>(
    '../servicios/servicioFlota',
  )
  return { ...real, listarFlota: (...args: unknown[]) => listarFlota(...args) }
})

vi.mock('../../choferes/transportistas/servicioTransportistas', async () => {
  const real = await vi.importActual<
    typeof import('../../choferes/transportistas/servicioTransportistas')
  >('../../choferes/transportistas/servicioTransportistas')
  return { ...real, listarTransportistas: (...args: unknown[]) => listarTransportistas(...args) }
})

vi.mock('../tiposVehiculo/servicioTiposVehiculo', async () => {
  const real = await vi.importActual<typeof import('../tiposVehiculo/servicioTiposVehiculo')>(
    '../tiposVehiculo/servicioTiposVehiculo',
  )
  return { ...real, listarTiposVehiculo: (...args: unknown[]) => listarTiposVehiculo(...args) }
})

function vehiculo(
  id: number,
  patente: string,
  estado: VehiculoListado['estado'] = 'fueraDeServicio',
  estadoDocumentacion: VehiculoListado['estadoDocumentacion'] = 'enRegla',
): VehiculoListado {
  return {
    id,
    patente,
    marca: 'Scania',
    modelo: 'R450',
    tipo: { id: 1, nombre: 'Tractor' },
    transportista: { id: 1, nombre: 'G&T Logística S.A.' },
    activo: true,
    estado,
    estadoDocumentacion,
  }
}

function pagina(
  items: VehiculoListado[],
  total = items.length,
  numero = 1,
): PaginaDe<VehiculoListado> {
  return { items, total, pagina: numero, tamanioPagina: 20 }
}

function renderizar() {
  return render(
    <MemoryRouter>
      <ListadoFlota />
    </MemoryRouter>,
  )
}

describe('ListadoFlota', () => {
  beforeEach(() => {
    listarFlota.mockReset()
    listarFlota.mockResolvedValue(pagina([vehiculo(1, 'AB123CD')]))
    listarTransportistas.mockReset()
    listarTransportistas.mockResolvedValue([])
    listarTiposVehiculo.mockReset()
    listarTiposVehiculo.mockResolvedValue([])
  })

  /** FR-036: dos situaciones distintas, dos mensajes distintos. */
  it('avisa que todavía no hay unidades registradas', async () => {
    listarFlota.mockResolvedValue(pagina([]))

    renderizar()

    expect(
      await screen.findByText('Todavía no hay unidades registradas. Registrá la primera para empezar.'),
    ).toBeInTheDocument()

    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('distingue "no hay ninguna" de "los filtros no encontraron nada" (FR-036)', async () => {
    listarFlota.mockResolvedValue(pagina([]))

    const usuario = userEvent.setup()
    renderizar()

    await screen.findByText('Todavía no hay unidades registradas. Registrá la primera para empezar.')

    await usuario.selectOptions(screen.getByLabelText('Estado del vehículo'), 'disponible')

    expect(
      await screen.findByText('Ningún vehículo coincide con los filtros aplicados.'),
    ).toBeInTheDocument()
  })

  /**
   * FR-037: **el control siempre dice qué está filtrando**. Sin el parámetro se devuelven sólo los
   * activos, y callárselo haría leer el listado como un error de datos (FR-031).
   */
  it('el control de estado dice que está mostrando sólo los activos (FR-037)', async () => {
    renderizar()

    await screen.findByRole('table')

    expect(
      screen.getByText(
        'Mostrando sólo las unidades activas. Elegí "Dado de baja" para ver las que salieron de la flota.',
      ),
    ).toBeInTheDocument()
  })

  it('el control dice qué estado está filtrando al elegir uno (FR-037)', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await screen.findByRole('table')

    await usuario.selectOptions(screen.getByLabelText('Estado del vehículo'), 'dadoDeBaja')

    expect(await screen.findByText('Mostrando sólo: Dado de baja.')).toBeInTheDocument()
  })

  /** El estado nunca se comunica sólo por color: el texto siempre acompaña (convención [003]). */
  it('muestra los estados con texto, no sólo con color', async () => {
    listarFlota.mockResolvedValue(
      pagina([
        vehiculo(1, 'AB123CD', 'disponible', 'enRegla'),
        vehiculo(2, 'CD234EF', 'fueraDeServicio', 'sinDocumentacion'),
        vehiculo(3, 'EF345GH', 'fueraDeServicio', 'vencida'),
      ]),
    )

    renderizar()

    // Se mira dentro de la tabla: los mismos textos están también en los filtros desplegables.
    const tabla = await screen.findByRole('table')

    expect(within(tabla).getByText('Disponible')).toBeInTheDocument()
    expect(within(tabla).getAllByText('Fuera de servicio')).toHaveLength(2)
    expect(within(tabla).getByText('En regla')).toBeInTheDocument()
    expect(within(tabla).getByText('Sin documentación')).toBeInTheDocument()
    expect(within(tabla).getByText('Vencida')).toBeInTheDocument()
  })

  it('vuelve a la página 1 al cambiar cualquier filtro (FR-032)', async () => {
    listarFlota.mockResolvedValue(pagina([vehiculo(1, 'AB123CD')], 73, 3))

    const usuario = userEvent.setup()
    renderizar()

    await screen.findByRole('table')
    listarFlota.mockClear()

    await usuario.selectOptions(screen.getByLabelText('Estado de documentación'), 'vencida')

    expect(listarFlota).toHaveBeenLastCalledWith(
      expect.objectContaining({ estadoDocumentacion: 'vencida' }),
      1,
    )
  })

  it('anuncia el cambio de página con role="status" (accesibilidad)', async () => {
    listarFlota.mockResolvedValue(pagina([vehiculo(1, 'AB123CD')], 73, 2))

    renderizar()

    const paginacion = await screen.findByRole('navigation', { name: 'Paginación' })

    expect(within(paginacion).getByRole('status')).toHaveTextContent(
      'Página 2 de 4, mostrando 21 a 40 de 73 vehículos',
    )
  })
})
