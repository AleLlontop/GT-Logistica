import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { FormularioVehiculo } from './FormularioVehiculo'

const crearVehiculo = vi.fn()
const listarTransportistas = vi.fn()
const listarTiposVehiculo = vi.fn()

vi.mock('../servicios/servicioFlota', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioFlota')>(
    '../servicios/servicioFlota',
  )
  return { ...real, crearVehiculo: (...args: unknown[]) => crearVehiculo(...args) }
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

function renderizar() {
  return render(
    <MemoryRouter>
      <FormularioVehiculo />
    </MemoryRouter>,
  )
}

describe('FormularioVehiculo (alta)', () => {
  beforeEach(() => {
    crearVehiculo.mockReset()
    crearVehiculo.mockResolvedValue({ id: 7 })

    listarTransportistas.mockReset()
    listarTransportistas.mockResolvedValue([
      { id: 1, nombre: 'G&T Logística S.A.', activo: true, choferesActivos: 0, vehiculosActivos: 0 },
    ])

    listarTiposVehiculo.mockReset()
    listarTiposVehiculo.mockResolvedValue([
      { id: 1, nombre: 'Tractor', activo: true, cantidadVehiculos: 0 },
    ])
  })

  /**
   * US2 esc. 8: **el alta no ofrece "Disponible"**. Una unidad recién registrada no tiene documentos,
   * así que su estado general es `sinDocumentacion` y `disponible` queda rechazado (FR-013, FR-014a).
   */
  it('no ofrece "Disponible" en el alta, y explica por qué (US2 esc. 8)', async () => {
    renderizar()

    const estado = await screen.findByLabelText('Estado operativo')

    expect(within(estado).queryByRole('option', { name: 'Disponible' })).not.toBeInTheDocument()
    expect(within(estado).getByRole('option', { name: 'Fuera de servicio' })).toBeInTheDocument()

    expect(
      screen.getByText(
        'Una unidad sin documentación cargada no puede quedar disponible. Cargá su documentación ' +
          'desde la ficha y después cambiá el estado.',
      ),
    ).toBeInTheDocument()
  })

  /** FR-004: la patente mal formada marca el campo con el motivo puntual, antes de enviar. */
  it('marca la patente con el motivo puntual cuando el formato es inválido (FR-004)', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await screen.findByLabelText('Patente')

    await usuario.type(screen.getByLabelText('Patente'), 'AB12CD')
    await usuario.type(screen.getByLabelText('Marca'), 'Scania')
    await usuario.type(screen.getByLabelText('Modelo'), 'R450')
    await usuario.selectOptions(screen.getByLabelText('Tipo de vehículo'), '1')
    await usuario.selectOptions(screen.getByLabelText('Transportista'), '1')

    await usuario.click(screen.getByRole('button', { name: 'Registrar unidad' }))

    expect(
      await screen.findByText('La patente tiene que tener el formato ABC123 o AB123CD.'),
    ).toBeInTheDocument()

    expect(screen.getByLabelText('Patente')).toHaveAttribute('aria-invalid', 'true')
    expect(crearVehiculo).not.toHaveBeenCalled()
  })

  /** FR-003: la patente se normaliza antes de enviar; quien escribe `ab 123 cd` guarda `AB123CD`. */
  it('normaliza la patente antes de enviarla (FR-003)', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await screen.findByLabelText('Patente')

    await usuario.type(screen.getByLabelText('Patente'), 'ab 123 cd')
    await usuario.type(screen.getByLabelText('Marca'), 'Scania')
    await usuario.type(screen.getByLabelText('Modelo'), 'R450')
    await usuario.selectOptions(screen.getByLabelText('Tipo de vehículo'), '1')
    await usuario.selectOptions(screen.getByLabelText('Transportista'), '1')

    await usuario.click(screen.getByRole('button', { name: 'Registrar unidad' }))

    expect(crearVehiculo).toHaveBeenCalledWith(
      expect.objectContaining({ patente: 'AB123CD', estadoOperativo: 'fueraDeServicio' }),
    )
  })

  /** US2 esc. 6: sin tipos activos no se puede registrar nada, y se dice por qué. */
  it('bloquea el alta y explica cuando no hay ningún tipo de vehículo (US2 esc. 6)', async () => {
    listarTiposVehiculo.mockResolvedValue([])

    renderizar()

    expect(
      await screen.findByText(
        'Todavía no hay ningún tipo de vehículo cargado. Pedile al administrador que cargue al ' +
          'menos uno antes de registrar unidades.',
      ),
    ).toBeInTheDocument()

    expect(screen.queryByLabelText('Patente')).not.toBeInTheDocument()
  })

  /** US2 esc. 7: lo mismo sin transportistas. */
  it('bloquea el alta y explica cuando no hay ningún transportista (US2 esc. 7)', async () => {
    listarTransportistas.mockResolvedValue([])

    renderizar()

    expect(
      await screen.findByText(
        'Todavía no hay ningún transportista cargado. Registrá al menos uno antes de registrar unidades.',
      ),
    ).toBeInTheDocument()

    expect(screen.queryByLabelText('Patente')).not.toBeInTheDocument()
  })

  /** FR-005 y FR-008a: los selectores ofrecen sólo lo activo. */
  it('pide sólo los tipos y transportistas activos (FR-005, FR-008a)', async () => {
    renderizar()

    await screen.findByLabelText('Patente')

    expect(listarTiposVehiculo).toHaveBeenCalledWith(true)
    expect(listarTransportistas).toHaveBeenCalledWith(undefined, true)
  })
})
