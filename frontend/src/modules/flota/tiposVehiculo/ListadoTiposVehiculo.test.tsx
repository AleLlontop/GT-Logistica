import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { ListadoTiposVehiculo } from './ListadoTiposVehiculo'
import type { TipoVehiculo } from './servicioTiposVehiculo'

const listarTiposVehiculo = vi.fn()
const crearTipoVehiculo = vi.fn()
const darDeBajaTipoVehiculo = vi.fn()
const reactivarTipoVehiculo = vi.fn()

vi.mock('./servicioTiposVehiculo', async () => {
  const real = await vi.importActual<typeof import('./servicioTiposVehiculo')>(
    './servicioTiposVehiculo',
  )

  return {
    ...real,
    listarTiposVehiculo: (...args: unknown[]) => listarTiposVehiculo(...args),
    crearTipoVehiculo: (...args: unknown[]) => crearTipoVehiculo(...args),
    darDeBajaTipoVehiculo: (...args: unknown[]) => darDeBajaTipoVehiculo(...args),
    reactivarTipoVehiculo: (...args: unknown[]) => reactivarTipoVehiculo(...args),
  }
})

function tipo(id: number, nombre: string, cantidadVehiculos = 0): TipoVehiculo {
  return { id, nombre, activo: true, cantidadVehiculos }
}

function tipoInactivo(id: number, nombre: string): TipoVehiculo {
  return { id, nombre, activo: false, cantidadVehiculos: 0 }
}

function renderizar() {
  return render(
    <MemoryRouter>
      <ListadoTiposVehiculo />
    </MemoryRouter>,
  )
}

describe('ListadoTiposVehiculo', () => {
  beforeEach(() => {
    listarTiposVehiculo.mockReset()
    listarTiposVehiculo.mockResolvedValue([tipo(1, 'Tractor')])
    crearTipoVehiculo.mockReset()
    crearTipoVehiculo.mockResolvedValue(tipo(2, 'Semirremolque'))
    darDeBajaTipoVehiculo.mockReset()
    reactivarTipoVehiculo.mockReset()
    reactivarTipoVehiculo.mockResolvedValue(tipo(1, 'Tractor'))
  })

  /**
   * US1 esc. 1 y FR-036: el catálogo arranca vacío, y sin al menos un tipo no se puede registrar
   * ninguna unidad. La pantalla lo dice en vez de mostrar una tabla vacía.
   */
  it('avisa que el catálogo está vacío, con el texto del contrato (US1 esc. 1)', async () => {
    listarTiposVehiculo.mockResolvedValue([])

    renderizar()

    expect(
      await screen.findByText(
        'Todavía no hay tipos de vehículo cargados. Cargá el primero para poder registrar unidades.',
      ),
    ).toBeInTheDocument()

    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('muestra los tipos con cuántos vehículos los usan (FR-010)', async () => {
    listarTiposVehiculo.mockResolvedValue([tipo(1, 'Tractor', 3)])

    renderizar()

    const tabla = await screen.findByRole('table')

    expect(tabla).toHaveTextContent('Tractor')
    expect(tabla).toHaveTextContent('3')
  })

  /** FR-009: el nombre es único, y el error se marca sobre el campo. */
  it('marca el campo cuando el nombre está duplicado (FR-009)', async () => {
    crearTipoVehiculo.mockRejectedValue(
      new ErrorHttp(400, {
        codigo: 'nombre_duplicado',
        mensaje: 'Ya existe un tipo con ese nombre.',
        campo: 'nombre',
      }),
    )

    const usuario = userEvent.setup()
    renderizar()

    await screen.findByRole('table')

    await usuario.type(screen.getByLabelText('Nombre'), 'Tractor')
    await usuario.click(screen.getByRole('button', { name: 'Cargar tipo' }))

    expect(await screen.findByText('Ya existe un tipo con ese nombre.')).toBeInTheDocument()
    expect(screen.getByLabelText('Nombre')).toHaveAttribute('aria-invalid', 'true')
  })

  /**
   * FR-010 y SC-008: la baja rechazada dice cuántos vehículos dependen del tipo. Saber que hay
   * dependencias sin saber cuántas no ayuda a resolverlo.
   */
  it('muestra el motivo cuando la baja se rechaza por vehículos asociados (FR-010)', async () => {
    darDeBajaTipoVehiculo.mockRejectedValue(
      new ErrorHttp(409, {
        codigo: 'tipo_vehiculo_en_uso',
        mensaje: 'No se puede dar de baja: 3 vehículo(s) usan este tipo.',
      }),
    )

    const usuario = userEvent.setup()
    renderizar()

    await screen.findByRole('table')

    await usuario.click(screen.getByRole('button', { name: 'Dar de baja' }))
    await usuario.click(screen.getByRole('button', { name: 'Confirmar' }))

    expect(
      await screen.findByText('No se puede dar de baja: 3 vehículo(s) usan este tipo.'),
    ).toBeInTheDocument()
  })

  /** SC-009: cancelar la confirmación no dispara ninguna petición. */
  it('cancelar la baja no llama al backend (SC-009)', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await screen.findByRole('table')

    await usuario.click(screen.getByRole('button', { name: 'Dar de baja' }))
    await usuario.click(screen.getByRole('button', { name: 'Cancelar' }))

    expect(darDeBajaTipoVehiculo).not.toHaveBeenCalled()
  })

  /**
   * FR-009: un tipo inactivo no desaparece del catálogo, y se lo vuelve a dar de alta desde su
   * edición. La opción sólo aparece ahí: la fila de un tipo activo no la ofrece.
   */
  it('ofrece dar de alta al editar un tipo inactivo (FR-009)', async () => {
    listarTiposVehiculo.mockResolvedValue([tipoInactivo(7, 'Utilitario')])

    const usuario = userEvent.setup()
    renderizar()

    await screen.findByRole('table')

    // Sin editar, la fila no ofrece ninguna acción de estado.
    expect(screen.queryByRole('button', { name: 'Dar de alta' })).not.toBeInTheDocument()
    expect(screen.queryByRole('button', { name: 'Dar de baja' })).not.toBeInTheDocument()

    await usuario.click(screen.getByRole('button', { name: 'Editar' }))

    expect(
      screen.getByText(
        'Este tipo está inactivo: no se ofrece al registrar vehículos. Podés darlo de alta de nuevo.',
      ),
    ).toBeInTheDocument()

    await usuario.click(screen.getByRole('button', { name: 'Dar de alta' }))

    expect(reactivarTipoVehiculo).toHaveBeenCalledWith(7)

    expect(
      await screen.findByText(
        'El tipo Utilitario volvió a estar activo. Se ofrece de nuevo al registrar vehículos.',
      ),
    ).toBeInTheDocument()
  })

  /** Editar uno activo no ofrece el alta: no hay nada que reactivar. */
  it('no ofrece dar de alta al editar un tipo activo', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await screen.findByRole('table')

    await usuario.click(screen.getByRole('button', { name: 'Editar' }))

    expect(screen.queryByRole('button', { name: 'Dar de alta' })).not.toBeInTheDocument()
  })
})
