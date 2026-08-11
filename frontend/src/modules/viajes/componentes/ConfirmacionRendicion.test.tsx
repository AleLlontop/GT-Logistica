import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { FichaViaje } from '../paginas/FichaViaje'
import type { ViajeDetalle } from '../servicios/servicioViajes'

const obtenerViaje = vi.fn()
const rendirViaje = vi.fn()

vi.mock('../servicios/servicioViajes', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioViajes')>(
    '../servicios/servicioViajes',
  )
  return {
    ...real,
    obtenerViaje: (...args: unknown[]) => obtenerViaje(...args),
    rendirViaje: (...args: unknown[]) => rendirViaje(...args),
  }
})

const EN_CURSO: ViajeDetalle = {
  id: 7,
  numero: 1041,
  fecha: '2026-08-10',
  cliente: { id: 1, nombre: 'Distribuidora del Litoral', activo: true },
  origen: 'Rosario',
  destino: 'Córdoba',
  chofer: { id: 3, nombre: 'Gómez, Juan', activo: true },
  vehiculo: { id: 5, nombre: 'AB123CD', activo: true },
  transportista: { id: 2, nombre: 'Transporte Sur', activo: true },
  estado: 'enCurso',
  importe: 0,
  demorado: false,
  esRetroactivo: false,
  motivoAnulacion: null,
  numeroRemito: null,
  detalleCarga: null,
  historial: [],
}

function renderizar() {
  return render(
    <MemoryRouter initialEntries={['/viajes/7']}>
      <Routes>
        <Route path="/viajes/:id" element={<FichaViaje puedeGestionar />} />
      </Routes>
    </MemoryRouter>,
  )
}

function rechazoPorConfirmacion() {
  return new ErrorHttp(409, {
    codigo: 'rendicion_requiere_confirmacion',
    mensaje:
      'El viaje va a quedar cerrado sin importe y después no se va a poder corregir. Confirmá para ' +
      'rendirlo igual.',
  })
}

describe('ConfirmacionRendicion', () => {
  beforeEach(() => {
    obtenerViaje.mockReset()
    obtenerViaje.mockResolvedValue(EN_CURSO)
    rendirViaje.mockReset()
  })

  /**
   * FR-038: **el diálogo lo dispara el `409` del backend, no la pantalla**. Es la diferencia con las
   * bajas del sistema, que se confirman acá porque se deshacen.
   */
  it('el diálogo aparece ante el 409 del backend', async () => {
    rendirViaje.mockRejectedValueOnce(rechazoPorConfirmacion())

    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Rendir' }))

    expect(
      await screen.findByRole('heading', { name: '¿Rendir el viaje 1041 sin importe?' }),
    ).toBeInTheDocument()

    expect(screen.getByText(/Después no se va a poder corregir/)).toBeInTheDocument()
  })

  /** US4 esc. 7: cancelar deja el viaje en curso y no dispara ninguna segunda petición. */
  it('cancelar no dispara una segunda petición', async () => {
    rendirViaje.mockRejectedValueOnce(rechazoPorConfirmacion())

    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Rendir' }))
    await screen.findByRole('dialog')

    rendirViaje.mockClear()

    await usuario.click(screen.getByRole('button', { name: 'Cancelar' }))

    expect(rendirViaje).not.toHaveBeenCalled()
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
  })

  it('confirmar reintenta con confirmado: true', async () => {
    rendirViaje.mockRejectedValueOnce(rechazoPorConfirmacion())
    rendirViaje.mockResolvedValueOnce({ ...EN_CURSO, estado: 'rendido' })

    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Rendir' }))
    await screen.findByRole('dialog')

    await usuario.click(screen.getByRole('button', { name: 'Rendir sin importe' }))

    expect(rendirViaje).toHaveBeenLastCalledWith(7, true)
    expect(await screen.findByText('El viaje 1041 quedó rendido.')).toBeInTheDocument()
  })

  /** Con importe mayor a cero rinde directo: no hay nada que confirmar (FR-038). */
  it('con importe no abre ningún diálogo', async () => {
    obtenerViaje.mockResolvedValue({ ...EN_CURSO, importe: 240000 })
    rendirViaje.mockResolvedValue({ ...EN_CURSO, importe: 240000, estado: 'rendido' })

    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(await screen.findByRole('button', { name: 'Rendir' }))

    expect(screen.queryByRole('dialog')).not.toBeInTheDocument()
    expect(await screen.findByText('El viaje 1041 quedó rendido.')).toBeInTheDocument()
  })
})
