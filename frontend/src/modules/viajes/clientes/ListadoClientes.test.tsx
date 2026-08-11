import { render, screen, within } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ListadoClientes } from './ListadoClientes'
import type { Cliente, PaginaDe } from './servicioClientes'

const listarClientes = vi.fn()

vi.mock('./servicioClientes', async () => {
  const real = await vi.importActual<typeof import('./servicioClientes')>('./servicioClientes')
  return { ...real, listarClientes: (...args: unknown[]) => listarClientes(...args) }
})

function cliente(id: number, razonSocial: string, activo = true): Cliente {
  return {
    id,
    razonSocial,
    cuit: '30712345678',
    telefono: '0341-555-5555',
    email: 'compras@litoral.com.ar',
    direccion: null,
    activo,
  }
}

function pagina(items: Cliente[], total = items.length, numero = 1): PaginaDe<Cliente> {
  return { items, total, pagina: numero, tamanioPagina: 20 }
}

function renderizar(puedeGestionar = true) {
  return render(
    <MemoryRouter>
      <ListadoClientes puedeGestionar={puedeGestionar} />
    </MemoryRouter>,
  )
}

describe('ListadoClientes', () => {
  beforeEach(() => {
    listarClientes.mockReset()
    listarClientes.mockResolvedValue(pagina([cliente(1, 'Distribuidora del Litoral')]))
  })

  /**
   * US1 esc. 1: el padrón arranca vacío en toda instalación nueva. La tabla sin filas se lee como un
   * error; el mensaje dice qué hacer.
   */
  it('avisa que el padrón está vacío y qué hacer', async () => {
    listarClientes.mockResolvedValue(pagina([]))

    renderizar()

    expect(
      await screen.findByText(
        'Todavía no hay clientes cargados. Registrá el primero para poder empezar a cargar viajes.',
      ),
    ).toBeInTheDocument()

    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  /**
   * Un cliente inactivo se muestra atenuado **y** con la palabra que lo explica: ningún estado se
   * comunica sólo por color (FR-049, convención [003]).
   */
  it('acompaña al cliente atenuado con la palabra Inactivo', async () => {
    listarClientes.mockResolvedValue(
      pagina([cliente(1, 'Activa S.A.'), cliente(2, 'La que se fue', false)]),
    )

    renderizar()

    const tabla = await screen.findByRole('table')

    expect(within(tabla).getByText(/La que se fue/)).toHaveTextContent('(inactivo)')
    expect(within(tabla).getByText('Inactivo')).toBeInTheDocument()
    expect(within(tabla).getByText('Activo')).toBeInTheDocument()
  })

  /** El inactivo ofrece *Dar de alta* y el activo *Dar de baja*, nunca los dos (FR-007). */
  it('ofrece dar de alta al inactivo y dar de baja al activo', async () => {
    listarClientes.mockResolvedValue(
      pagina([cliente(1, 'Activa S.A.'), cliente(2, 'La que se fue', false)]),
    )

    renderizar()

    await screen.findByRole('table')

    expect(screen.getByRole('button', { name: 'Dar de baja' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Dar de alta' })).toBeInTheDocument()
  })

  /**
   * FR-052: quien tiene sólo `viajes.consultar` ve el padrón entero y ningún botón de escritura.
   * Ocultarlos es una cortesía; la restricción la aplica el servidor (SC-012).
   */
  it('sin permiso de gestión no ofrece ninguna acción de escritura', async () => {
    listarClientes.mockResolvedValue(
      pagina([cliente(1, 'Activa S.A.'), cliente(2, 'La que se fue', false)]),
    )

    renderizar(false)

    const tabla = await screen.findByRole('table')

    expect(within(tabla).getByText(/Activa S.A./)).toBeInTheDocument()
    expect(screen.queryByRole('link', { name: 'Nuevo cliente' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Editar' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Dar de baja' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Dar de alta' })).not.toBeInTheDocument()
  })

  it('anuncia el cambio de página con role="status"', async () => {
    listarClientes.mockResolvedValue(pagina([cliente(1, 'Distribuidora del Litoral')], 73, 2))

    renderizar()

    const paginacion = await screen.findByRole('navigation', { name: 'Paginación' })

    expect(within(paginacion).getByRole('status')).toHaveTextContent(
      'Página 2 de 4, mostrando 21 a 40 de 73 clientes',
    )
  })
})
