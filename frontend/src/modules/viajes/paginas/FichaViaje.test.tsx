import { render, screen } from '@testing-library/react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { FichaViaje } from './FichaViaje'
import type { ViajeDetalle } from '../servicios/servicioViajes'

const obtenerViaje = vi.fn()

vi.mock('../servicios/servicioViajes', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioViajes')>(
    '../servicios/servicioViajes',
  )
  return { ...real, obtenerViaje: (...args: unknown[]) => obtenerViaje(...args) }
})

const VIAJE: ViajeDetalle = {
  id: 7,
  numero: 1041,
  fecha: '2026-08-10',
  cliente: { id: 1, nombre: 'Distribuidora del Litoral', activo: true },
  origen: 'Rosario',
  destino: 'Rosario',
  chofer: null,
  vehiculo: null,
  transportista: null,
  estado: 'pendiente',
  importe: 125000,
  demorado: false,
  esRetroactivo: false,
  motivoAnulacion: null,
  numeroRemito: 'R-0001',
  detalleCarga: null,
  historial: [
    {
      estadoAnterior: null,
      estadoNuevo: 'pendiente',
      usuario: 'Tráfico',
      ocurridoEn: '2026-08-10T13:00:00Z',
    },
  ],
}

function renderizar(estado: unknown) {
  return render(
    <MemoryRouter initialEntries={[{ pathname: '/viajes/7', state: estado }]}>
      <Routes>
        <Route path="/viajes/:id" element={<FichaViaje puedeGestionar />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('FichaViaje', () => {
  beforeEach(() => {
    obtenerViaje.mockReset()
    obtenerViaje.mockResolvedValue(VIAJE)
  })

  /**
   * FR-015a y US2 esc. 10: la advertencia que no bloquea llega **con** el resultado del guardado, que
   * ocurrió en el formulario. Se anuncia junto a la confirmación y como `role="status"`, nunca como
   * error: el viaje ya quedó registrado y no hay ningún paso extra que dar.
   */
  it('anuncia la advertencia del alta junto con la confirmación, sin tratarla como error', async () => {
    renderizar({
      aviso: 'El viaje 1041 quedó registrado como pendiente.',
      advertencias: [
        {
          codigo: 'origen_igual_a_destino',
          mensaje:
            'El origen y el destino son la misma localidad. Si es un servicio dentro de la ciudad, está bien.',
        },
      ],
    })

    const confirmacion = await screen.findByText('El viaje 1041 quedó registrado como pendiente.')
    const advertencia = screen.getByText(/El origen y el destino son la misma localidad/)

    expect(confirmacion).toHaveAttribute('role', 'status')
    expect(advertencia).toHaveAttribute('role', 'status')
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  it('sin advertencias muestra sólo la confirmación', async () => {
    renderizar({ aviso: 'El viaje 1041 quedó registrado como pendiente.', advertencias: [] })

    expect(
      await screen.findByText('El viaje 1041 quedó registrado como pendiente.'),
    ).toBeInTheDocument()
    expect(screen.queryByText(/misma localidad/)).not.toBeInTheDocument()
  })
})
