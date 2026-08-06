import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ListadoChoferes } from './ListadoChoferes'
import type { ChoferListado, PaginaDe } from '../servicios/servicioChoferes'

const listarChoferes = vi.fn()
const listarTransportistas = vi.fn()

vi.mock('../servicios/servicioChoferes', async () => {
  const real = await vi.importActual<
    typeof import('../servicios/servicioChoferes')
  >('../servicios/servicioChoferes')
  return { ...real, listarChoferes: (...args: unknown[]) => listarChoferes(...args) }
})

vi.mock('../transportistas/servicioTransportistas', async () => {
  const real = await vi.importActual<
    typeof import('../transportistas/servicioTransportistas')
  >('../transportistas/servicioTransportistas')
  return { ...real, listarTransportistas: (...args: unknown[]) => listarTransportistas(...args) }
})

function chofer(id: number, apellido: string, estado: ChoferListado['estadoDocumentacion']): ChoferListado {
  return {
    id,
    apellido,
    nombre: 'Ana',
    dni: `3011122${id}`,
    transportista: { id: 1, nombre: 'G&T Logística S.A.' },
    activo: true,
    estadoDocumentacion: estado,
  }
}

function pagina(items: ChoferListado[], total = items.length, numero = 1): PaginaDe<ChoferListado> {
  return { items, total, pagina: numero, tamanioPagina: 20 }
}

function renderizar() {
  return render(
    <MemoryRouter>
      <ListadoChoferes />
    </MemoryRouter>,
  )
}

describe('ListadoChoferes', () => {
  beforeEach(() => {
    listarChoferes.mockReset()
    listarChoferes.mockResolvedValue(pagina([chofer(1, 'González', 'enRegla')]))
    listarTransportistas.mockReset()
    listarTransportistas.mockResolvedValue([])
  })

  it('avisa que todavía no hay choferes registrados (FR-023)', async () => {
    listarChoferes.mockResolvedValue(pagina([]))

    renderizar()

    expect(await screen.findByText('Todavía no hay choferes registrados.')).toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('distingue "no hay ninguno" de "los filtros no encontraron nada" (FR-023)', async () => {
    listarChoferes.mockResolvedValue(pagina([]))

    const usuario = userEvent.setup()
    renderizar()

    await screen.findByText('Todavía no hay choferes registrados.')

    await usuario.type(screen.getByLabelText('Apellido'), 'inexistente')

    expect(
      await screen.findByText('No hay choferes que coincidan con los filtros aplicados.'),
    ).toBeInTheDocument()
  })

  /**
   * FR-022: el filtro de estado arranca en `Activo`, **a la vista**. Un listado que oculta choferes
   * sin decirlo se lee como un error de datos.
   */
  it('arranca filtrando por Activo, con el control a la vista (FR-022)', async () => {
    renderizar()

    await screen.findByRole('table')

    expect(screen.getByLabelText('Estado')).toHaveValue('activo')
    expect(listarChoferes).toHaveBeenCalledWith(
      expect.objectContaining({ estado: 'activo' }),
      1,
    )
  })

  /** El estado nunca se comunica sólo por color: el texto siempre acompaña (accesibilidad). */
  it('muestra el estado de documentación con texto, no sólo con color', async () => {
    listarChoferes.mockResolvedValue(
      pagina([
        chofer(1, 'González', 'enRegla'),
        chofer(2, 'Pérez', 'sinDocumentacion'),
        chofer(3, 'Rodríguez', 'vencida'),
      ]),
    )

    renderizar()

    // Se mira dentro de la tabla: los mismos textos están también en el filtro desplegable.
    const tabla = await screen.findByRole('table')

    expect(within(tabla).getByText('En regla')).toBeInTheDocument()
    // `Sin documentación` es una cuarta situación, distinta de estar en regla (FR-028).
    expect(within(tabla).getByText('Sin documentación')).toBeInTheDocument()
    expect(within(tabla).getByText('Vencida')).toBeInTheDocument()
  })

  it('vuelve a la página 1 al cambiar cualquier filtro (FR-030)', async () => {
    listarChoferes.mockResolvedValue(pagina([chofer(1, 'González', 'enRegla')], 73, 3))

    const usuario = userEvent.setup()
    renderizar()

    await screen.findByRole('table')
    listarChoferes.mockClear()

    await usuario.type(screen.getByLabelText('Apellido'), 'G')

    expect(listarChoferes).toHaveBeenLastCalledWith(expect.objectContaining({ apellido: 'G' }), 1)
  })

  it('anuncia el cambio de página con role="status" (accesibilidad)', async () => {
    listarChoferes.mockResolvedValue(pagina([chofer(1, 'González', 'enRegla')], 73, 2))

    renderizar()

    const paginacion = await screen.findByRole('navigation', { name: 'Paginación' })

    expect(within(paginacion).getByRole('status')).toHaveTextContent(
      'Página 2 de 4, mostrando 21 a 40 de 73 choferes',
    )
  })
})
