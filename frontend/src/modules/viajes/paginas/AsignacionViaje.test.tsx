import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { AsignacionViaje } from './AsignacionViaje'
import type { Asignables, ViajeDetalle } from '../servicios/servicioViajes'

const obtenerViaje = vi.fn()
const listarAsignables = vi.fn()
const asignarChoferYVehiculo = vi.fn()

vi.mock('../servicios/servicioViajes', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioViajes')>(
    '../servicios/servicioViajes',
  )
  return {
    ...real,
    obtenerViaje: (...args: unknown[]) => obtenerViaje(...args),
    listarAsignables: (...args: unknown[]) => listarAsignables(...args),
    asignarChoferYVehiculo: (...args: unknown[]) => asignarChoferYVehiculo(...args),
  }
})

const VIAJE: ViajeDetalle = {
  id: 7,
  numero: 1041,
  fecha: '2026-08-10',
  cliente: { id: 1, nombre: 'Distribuidora del Litoral', activo: true },
  origen: 'Rosario',
  destino: 'Córdoba',
  chofer: null,
  vehiculo: null,
  transportista: null,
  estado: 'pendiente',
  importe: 0,
  demorado: false,
  esRetroactivo: false,
  motivoAnulacion: null,
  numeroRemito: null,
  detalleCarga: null,
  historial: [],
}

const ASIGNABLES: Asignables = {
  choferes: [{ id: 3, nombre: 'Gómez, Juan', observacion: null }],
  vehiculos: [{ id: 5, nombre: 'AB123CD', observacion: null }],
}

const CHOFER_ASIGNADO = { id: 3, nombre: 'Gómez, Juan', activo: true }
const VEHICULO_ASIGNADO = { id: 5, nombre: 'AB123CD', activo: true }

function renderizar() {
  return render(
    <MemoryRouter initialEntries={['/viajes/7/asignacion']}>
      <Routes>
        <Route path="/viajes/:id/asignacion" element={<AsignacionViaje />} />
      </Routes>
    </MemoryRouter>,
  )
}

describe('AsignacionViaje', () => {
  beforeEach(() => {
    obtenerViaje.mockReset()
    obtenerViaje.mockResolvedValue(VIAJE)
    listarAsignables.mockReset()
    listarAsignables.mockResolvedValue(ASIGNABLES)
    asignarChoferYVehiculo.mockReset()
    asignarChoferYVehiculo.mockResolvedValue({
      viaje: { ...VIAJE, chofer: CHOFER_ASIGNADO, vehiculo: VEHICULO_ASIGNADO },
      advertencias: [],
    })
  })

  /**
   * FR-019b: no hay asignación parcial. El botón deshabilitado con una sola unidad elegida es lo que
   * impide que alguien deje un viaje con chofer y sin vehículo.
   */
  it('el botón no se habilita con una sola unidad elegida', async () => {
    const usuario = userEvent.setup()
    renderizar()

    const boton = await screen.findByRole('button', { name: 'Asignar' })
    expect(boton).toBeDisabled()

    await usuario.selectOptions(screen.getByLabelText('Chofer'), '3')
    expect(boton).toBeDisabled()

    await usuario.selectOptions(screen.getByLabelText('Vehículo'), '5')
    expect(boton).toBeEnabled()
  })

  /** SC-014: sin decir contra qué fecha valida, un rechazo sobre un viaje retroactivo confunde. */
  it('dice contra qué fecha se valida la documentación', async () => {
    renderizar()

    expect(
      await screen.findByText('La documentación se valida contra la fecha del viaje: 10/08/2026.'),
    ).toBeInTheDocument()
  })

  /**
   * FR-023 y FR-015a: la advertencia llega **con** el resultado. La asignación ya se guardó, así que
   * se anuncia con `role="status"` junto al éxito, nunca como error.
   */
  it('la advertencia por documento próximo a vencer no impide que la asignación se haya guardado', async () => {
    asignarChoferYVehiculo.mockResolvedValue({
      viaje: { ...VIAJE, chofer: CHOFER_ASIGNADO, vehiculo: VEHICULO_ASIGNADO },
      advertencias: [
        {
          codigo: 'documentacion_proxima_a_vencer',
          mensaje: 'Asignación guardada. Atención: Licencia de Gómez, Juan vence el 20/08/2026.',
        },
      ],
    })

    const usuario = userEvent.setup()
    renderizar()

    await usuario.selectOptions(await screen.findByLabelText('Chofer'), '3')
    await usuario.selectOptions(screen.getByLabelText('Vehículo'), '5')
    await usuario.click(screen.getByRole('button', { name: 'Asignar' }))

    expect(
      await screen.findByText('El viaje 1041 quedó asignado a Gómez, Juan con AB123CD.'),
    ).toBeInTheDocument()

    expect(await screen.findByText(/vence el 20\/08\/2026/)).toBeInTheDocument()
    expect(screen.queryByRole('alert')).not.toBeInTheDocument()
  })

  /** Las listas vacías son una respuesta legítima: la pantalla dice qué falta cargar (FR-021). */
  it('avisa cuando no hay choferes ni vehículos para ofrecer', async () => {
    listarAsignables.mockResolvedValue({ choferes: [], vehiculos: [] })

    renderizar()

    expect(
      await screen.findByText(
        'Todavía no hay choferes activos. Cargá al menos uno en el módulo de Choferes.',
      ),
    ).toBeInTheDocument()

    expect(
      screen.getByText('Todavía no hay vehículos disponibles. Revisá el módulo de Flota.'),
    ).toBeInTheDocument()
  })

  /**
   * FR-021 y SC-014: la unidad con documentación vencida **se sigue ofreciendo**, porque el filtro es
   * el estado operativo guardado. Pero se ofrece con el motivo escrito al lado: sin eso, el
   * desplegable contradice al módulo de Flota, que la muestra fuera de servicio, y quien opera elige
   * a ciegas una unidad que el servidor va a rechazar.
   */
  it('ofrece la unidad observada con el motivo al lado', async () => {
    listarAsignables.mockResolvedValue({
      choferes: ASIGNABLES.choferes,
      vehiculos: [
        { id: 5, nombre: 'AB123CD', observacion: null },
        { id: 9, nombre: 'EF456GH', observacion: 'Seguro vencido el 10/08/2026' },
      ],
    })

    renderizar()

    const vehiculos = await screen.findByLabelText('Vehículo')

    expect(vehiculos).toHaveTextContent('EF456GH — Seguro vencido el 10/08/2026')
    expect(
      screen.getByRole('option', { name: 'EF456GH — Seguro vencido el 10/08/2026' }),
    ).toBeEnabled()
    expect(screen.getByRole('option', { name: 'AB123CD' })).toBeInTheDocument()
  })

  /**
   * La observación se calcula contra la fecha del viaje, así que la pantalla tiene que mandarla: con
   * la de hoy, un viaje retroactivo mostraría observada una unidad que ese día estaba en regla.
   */
  it('pide la lista con la fecha del viaje', async () => {
    renderizar()

    await screen.findByLabelText('Vehículo')

    expect(listarAsignables).toHaveBeenCalledWith('2026-08-10')
  })
})
