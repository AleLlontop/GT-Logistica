import { render, screen } from '@testing-library/react'
import { MemoryRouter } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { FormularioDocumentoVehiculo } from './FormularioDocumentoVehiculo'

const listarTipos = vi.fn()

vi.mock('../../choferes/documentacion/servicioTipos', async () => {
  const real = await vi.importActual<typeof import('../../choferes/documentacion/servicioTipos')>(
    '../../choferes/documentacion/servicioTipos',
  )
  return { ...real, listarTipos: (...args: unknown[]) => listarTipos(...args) }
})

function renderizar() {
  return render(
    <MemoryRouter>
      <FormularioDocumentoVehiculo
        vehiculoId={1}
        documentosDelVehiculo={[]}
        onGuardado={() => {}}
        onCancelar={() => {}}
      />
    </MemoryRouter>,
  )
}

describe('FormularioDocumentoVehiculo', () => {
  beforeEach(() => {
    listarTipos.mockReset()
    listarTipos.mockResolvedValue([
      {
        id: 10,
        nombre: 'Seguro',
        diasAvisoVencimiento: 30,
        ambito: 'vehiculo',
        activo: true,
        documentosAsociados: 0,
      },
    ])
  })

  /**
   * FR-021 y SC-004: **no existe ningún control de estado**, ni editable ni de sólo lectura. El
   * sistema lo calcula y no lo recibe por ninguna vía.
   */
  it('no tiene ningún control de estado (FR-021, SC-004)', async () => {
    renderizar()

    await screen.findByLabelText('Tipo de documentación')

    expect(screen.queryByLabelText(/estado/i)).not.toBeInTheDocument()
    expect(screen.queryByRole('combobox', { name: /estado/i })).not.toBeInTheDocument()

    // Y tampoco aparecen los valores como texto previsto, para no dar a entender que se eligen.
    expect(screen.queryByText('Vigente')).not.toBeInTheDocument()
    expect(screen.queryByText('Próxima a vencer')).not.toBeInTheDocument()
  })

  /** FR-016a: el archivo es opcional, y la etiqueta lo dice. */
  it('etiqueta el archivo como opcional, con las restricciones a la vista (FR-016a, FR-025)', async () => {
    renderizar()

    expect(await screen.findByLabelText('Archivo (opcional)')).toBeInTheDocument()
    expect(screen.getByText('PDF, JPG o PNG, hasta 10 MB')).toBeInTheDocument()
  })

  /**
   * FR-017a: el selector pide **sólo los tipos activos de ámbito vehículo**. Los de chofer no
   * aparecen, ni siquiera como opción deshabilitada.
   */
  it('pide únicamente los tipos activos de ámbito vehículo (FR-017a)', async () => {
    renderizar()

    await screen.findByLabelText('Tipo de documentación')

    expect(listarTipos).toHaveBeenCalledWith(true, 'vehiculo')
  })

  it('avisa y enlaza cuando no hay tipos de ámbito vehículo cargados (FR-017a)', async () => {
    listarTipos.mockResolvedValue([])

    renderizar()

    expect(
      await screen.findByText(
        'No hay tipos de documentación de vehículo activos. Cargá uno desde la pantalla Tipos de ' +
          'documentación, con ámbito Vehículo.',
      ),
    ).toBeInTheDocument()

    expect(screen.getByRole('link', { name: 'Ir a Tipos de documentación' })).toBeInTheDocument()
    expect(screen.queryByLabelText('Número')).not.toBeInTheDocument()
  })
})
