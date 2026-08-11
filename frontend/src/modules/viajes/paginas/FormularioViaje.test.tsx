import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { FormularioViaje } from './FormularioViaje'
import type { Cliente, PaginaDe } from '../clientes/servicioClientes'

const listarClientes = vi.fn()
const crearViaje = vi.fn()

vi.mock('../clientes/servicioClientes', async () => {
  const real = await vi.importActual<typeof import('../clientes/servicioClientes')>(
    '../clientes/servicioClientes',
  )
  return { ...real, listarClientes: (...args: unknown[]) => listarClientes(...args) }
})

vi.mock('../servicios/servicioViajes', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioViajes')>(
    '../servicios/servicioViajes',
  )
  return { ...real, crearViaje: (...args: unknown[]) => crearViaje(...args) }
})

function cliente(id: number, razonSocial: string): Cliente {
  return {
    id,
    razonSocial,
    cuit: '30712345678',
    telefono: '0341-555-5555',
    email: 'compras@litoral.com.ar',
    direccion: null,
    activo: true,
  }
}

function paginaDeClientes(items: Cliente[]): PaginaDe<Cliente> {
  return { items, total: items.length, pagina: 1, tamanioPagina: 20 }
}

const ADVERTENCIA_RETROACTIVA = {
  codigo: 'carga_retroactiva',
  mensaje:
    'Estás cargando un viaje con fecha anterior a hoy. Queda registrado como carga retroactiva.',
}

const ADVERTENCIA_ORIGEN_IGUAL_A_DESTINO = {
  codigo: 'origen_igual_a_destino',
  mensaje:
    'El origen y el destino son la misma localidad. Si es un servicio dentro de la ciudad, está bien.',
}

/**
 * Doble de la ficha: muestra a dónde navegó el formulario y qué se llevó con la navegación —la
 * confirmación y las advertencias—, que es lo que la ficha real anuncia con `role="status"`.
 */
function FichaDePrueba() {
  const ubicacion = useLocation()
  const estado = ubicacion.state as {
    aviso?: string
    advertencias?: { codigo: string; mensaje: string }[]
  } | null

  return (
    <div>
      <p data-testid="ruta">{ubicacion.pathname}</p>
      <div data-testid="estado">
        <p>{estado?.aviso}</p>
        {(estado?.advertencias ?? []).map((advertencia) => (
          <p key={advertencia.codigo}>{advertencia.mensaje}</p>
        ))}
      </div>
    </div>
  )
}

function renderizar() {
  return render(
    <MemoryRouter initialEntries={['/viajes/nuevo']}>
      <Routes>
        <Route path="/viajes/nuevo" element={<FormularioViaje />} />
        <Route path="/viajes/:id" element={<FichaDePrueba />} />
      </Routes>
    </MemoryRouter>,
  )
}

async function completar(usuario: ReturnType<typeof userEvent.setup>) {
  await usuario.selectOptions(await screen.findByLabelText('Cliente'), '1')
  await usuario.type(screen.getByLabelText('Fecha del viaje'), '2026-08-10')
  await usuario.type(screen.getByLabelText('Origen'), 'Rosario')
  await usuario.type(screen.getByLabelText('Destino'), 'Córdoba')
}

describe('FormularioViaje', () => {
  beforeEach(() => {
    listarClientes.mockReset()
    listarClientes.mockResolvedValue(paginaDeClientes([cliente(1, 'Distribuidora del Litoral')]))
    crearViaje.mockReset()
    crearViaje.mockResolvedValue({ viaje: { id: 7, numero: 1041 }, advertencias: [] })
  })

  /**
   * FR-019a y US3 esc. 14: el viaje se registra primero y se asigna después, desde su propia
   * pantalla. Que el formulario no los ofrezca es lo que hace que corregir un destino no pueda tocar
   * quién maneja.
   */
  it('no ofrece chofer ni vehículo', async () => {
    renderizar()

    await screen.findByLabelText('Cliente')

    expect(screen.queryByLabelText(/chofer/i)).not.toBeInTheDocument()
    expect(screen.queryByLabelText(/veh[íi]culo/i)).not.toBeInTheDocument()
  })

  /** FR-011, FR-017: el número lo genera el sistema y no es editable en ningún estado. */
  it('no ofrece ningún control para el número de viaje', async () => {
    renderizar()

    await screen.findByLabelText('Cliente')

    expect(screen.queryByLabelText(/n[úu]mero de viaje/i)).not.toBeInTheDocument()
  })

  /**
   * US2 esc. 3: sin ningún cliente activo cargado no se puede registrar un viaje, y la pantalla lo
   * dice con el camino para resolverlo en vez de dejar un desplegable vacío.
   */
  it('sin clientes activos no deja completar el alta y ofrece ir a Clientes', async () => {
    listarClientes.mockResolvedValue(paginaDeClientes([]))

    renderizar()

    expect(
      await screen.findByText(
        'Todavía no hay clientes activos. Cargá al menos un cliente antes de registrar viajes.',
      ),
    ).toBeInTheDocument()

    expect(screen.getByRole('link', { name: 'Ir a Clientes' })).toBeInTheDocument()
    expect(screen.getByRole('button', { name: 'Guardar viaje' })).toBeDisabled()
  })

  /** FR-013: el cero es válido —viaje sin cargo— y el negativo se marca en el campo. */
  it('acepta el importe en cero', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await completar(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Guardar viaje' }))

    expect(crearViaje).toHaveBeenCalledWith(expect.objectContaining({ importe: 0 }))
  })

  it('marca el campo importe cuando el backend rechaza el negativo', async () => {
    crearViaje.mockRejectedValue(
      new ErrorHttp(400, {
        codigo: 'importe_negativo',
        mensaje: 'El importe no puede ser negativo.',
        campo: 'importe',
      }),
    )

    const usuario = userEvent.setup()
    renderizar()

    await completar(usuario)
    await usuario.clear(screen.getByLabelText('Importe en pesos'))
    await usuario.type(screen.getByLabelText('Importe en pesos'), '-1')
    await usuario.click(screen.getByRole('button', { name: 'Guardar viaje' }))

    expect(await screen.findByText('El importe no puede ser negativo.')).toBeInTheDocument()
    expect(screen.getByLabelText('Importe en pesos')).toHaveAttribute('aria-invalid', 'true')
  })

  /**
   * FR-015a: la advertencia llega **con** el resultado. El viaje ya se guardó, así que acompaña a la
   * confirmación hasta la ficha en vez de dejar al operador frente al formulario de alta.
   */
  it('lleva la advertencia de carga retroactiva a la ficha, junto con la confirmación', async () => {
    crearViaje.mockResolvedValue({
      viaje: { id: 7, numero: 1041 },
      advertencias: [ADVERTENCIA_RETROACTIVA],
    })

    const usuario = userEvent.setup()
    renderizar()

    await completar(usuario)
    await usuario.click(screen.getByRole('button', { name: 'Guardar viaje' }))

    expect(await screen.findByTestId('ruta')).toHaveTextContent('/viajes/7')
    expect(screen.getByTestId('estado')).toHaveTextContent(
      'El viaje 1041 quedó registrado como pendiente.',
    )
    expect(screen.getByTestId('estado')).toHaveTextContent(/Queda registrado como carga retroactiva/)
  })

  /**
   * El bug que motivó esto: con una advertencia el formulario se quedaba en pantalla en modo alta, así
   * que volver a apretar *Guardar viaje* mandaba un **segundo** alta —y rebotaba por remito duplicado
   * contra el viaje recién creado—. Guardar sale del formulario aunque haya advertencias (US2 esc. 10).
   */
  it('no deja repetir el alta después de guardar con advertencia', async () => {
    crearViaje.mockResolvedValue({
      viaje: { id: 7, numero: 1041 },
      advertencias: [ADVERTENCIA_ORIGEN_IGUAL_A_DESTINO],
    })

    const usuario = userEvent.setup()
    renderizar()

    await completar(usuario)
    await usuario.type(screen.getByLabelText('Número de remito (opcional)'), 'R-0001')
    await usuario.click(screen.getByRole('button', { name: 'Guardar viaje' }))

    await screen.findByTestId('ruta')

    expect(screen.queryByRole('button', { name: 'Guardar viaje' })).not.toBeInTheDocument()
    expect(crearViaje).toHaveBeenCalledTimes(1)
  })
})
