import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { TotalesPeriodo } from './TotalesPeriodo'

const consultarTotales = vi.fn()

vi.mock('../servicios/servicioViajes', async () => {
  const real = await vi.importActual<typeof import('../servicios/servicioViajes')>(
    '../servicios/servicioViajes',
  )
  return { ...real, consultarTotales: (...args: unknown[]) => consultarTotales(...args) }
})

async function elegirRango(usuario: ReturnType<typeof userEvent.setup>) {
  await usuario.type(screen.getByLabelText('Desde'), '2026-08-01')
  await usuario.type(screen.getByLabelText('Hasta'), '2026-08-31')
}

describe('TotalesPeriodo', () => {
  beforeEach(() => {
    consultarTotales.mockReset()
    consultarTotales.mockResolvedValue({
      porCliente: [
        { id: 1, nombre: 'Distribuidora del Litoral', cantidadViajes: 8, importeTotal: 1_240_000 },
      ],
      porTransportista: [
        { id: 2, nombre: 'Transporte Sur', cantidadViajes: 5, importeTotal: 780_000 },
      ],
    })
  })

  /**
   * US7 esc. 2: **sin rango no se calcula ni se muestra ningún total**, y el mensaje lo dice. Un
   * total "de todo" no responde ninguna pregunta real (FR-046a).
   */
  it('sin rango elegido no calcula nada y lo dice', () => {
    render(<TotalesPeriodo />)

    expect(screen.getByText('Elegí un rango de fechas para ver los totales.')).toBeInTheDocument()
    expect(consultarTotales).not.toHaveBeenCalled()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })

  it('con una sola fecha tampoco calcula', async () => {
    const usuario = userEvent.setup()
    render(<TotalesPeriodo />)

    await usuario.type(screen.getByLabelText('Desde'), '2026-08-01')

    expect(screen.getByText('Elegí un rango de fechas para ver los totales.')).toBeInTheDocument()
    expect(consultarTotales).not.toHaveBeenCalled()
  })

  it('con el rango elegido muestra los dos cuadros', async () => {
    const usuario = userEvent.setup()
    render(<TotalesPeriodo />)

    await elegirRango(usuario)

    const porCliente = await screen.findByRole('table', { name: 'Por cliente' })
    const porTransportista = screen.getByRole('table', { name: 'Por transportista' })

    expect(within(porCliente).getByText('Distribuidora del Litoral')).toBeInTheDocument()
    expect(within(porCliente).getByText('8')).toBeInTheDocument()
    expect(within(porTransportista).getByText('Transporte Sur')).toBeInTheDocument()
  })

  /** Principio II: los importes van en pesos argentinos, con punto de miles y coma decimal. */
  it('formatea los importes con formatearPesos', async () => {
    const usuario = userEvent.setup()
    render(<TotalesPeriodo />)

    await elegirRango(usuario)

    expect(await screen.findByText('$ 1.240.000,00')).toBeInTheDocument()
    expect(screen.getByText('$ 780.000,00')).toBeInTheDocument()
  })

  it('avisa cuando no hay viajes en el período', async () => {
    consultarTotales.mockResolvedValue({ porCliente: [], porTransportista: [] })

    const usuario = userEvent.setup()
    render(<TotalesPeriodo />)

    await elegirRango(usuario)

    expect(await screen.findByText('No hay viajes en el período elegido.')).toBeInTheDocument()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })
})
