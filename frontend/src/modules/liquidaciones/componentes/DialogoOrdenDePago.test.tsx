import { fireEvent, render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { DialogoOrdenDePago } from './DialogoOrdenDePago'

const onEnviar = vi.fn()
const onRegistrada = vi.fn()
const onCancelar = vi.fn()

function renderizar() {
  render(
    <DialogoOrdenDePago
      numero="LQ-12"
      transportista="Transportes Díaz"
      restaPagar={155_000}
      fechaGeneracion="2026-09-14"
      hoy="2026-09-20"
      onEnviar={onEnviar}
      onRegistrada={onRegistrada}
      onCancelar={onCancelar}
    />,
  )
}

describe('DialogoOrdenDePago', () => {
  beforeEach(() => {
    onEnviar.mockReset().mockResolvedValue({
      tipo: 'requiereConfirmacion',
      confirmacion: { importe: 100_000, restaPagarAntes: 155_000, restaPagarDespues: 55_000, quedaPagada: false },
    })
    onRegistrada.mockReset()
    onCancelar.mockReset()
  })

  /** FR-036: propone hoy y lo que resta pagar. */
  it('propone la fecha de hoy y lo que resta pagar', () => {
    renderizar()

    expect(screen.getByLabelText('Fecha de pago')).toHaveValue('2026-09-20')
    expect(screen.getByLabelText('Importe')).toHaveValue('155.000,00')
    expect(screen.getByText('LQ-12 · Transportes Díaz · Resta pagar $ 155.000,00')).toBeInTheDocument()
  })

  it('marca el importe en cero y no envía', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.clear(screen.getByLabelText('Importe'))
    await usuario.type(screen.getByLabelText('Importe'), '0')
    await usuario.click(screen.getByRole('button', { name: 'Registrar orden de pago' }))

    expect(screen.getByText('Escribí un importe mayor que cero. Ejemplo: 120000,00')).toBeInTheDocument()
    expect(onEnviar).not.toHaveBeenCalled()
  })

  it('marca el importe que supera lo que resta', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.clear(screen.getByLabelText('Importe'))
    await usuario.type(screen.getByLabelText('Importe'), '160000')
    await usuario.click(screen.getByRole('button', { name: 'Registrar orden de pago' }))

    expect(screen.getByText('El importe supera lo que resta pagar: $ 155.000,00.')).toBeInTheDocument()
    expect(onEnviar).not.toHaveBeenCalled()
  })

  /** FR-038: fuera del rango se marca con el rango. */
  it('marca una fecha anterior a la generación', async () => {
    const usuario = userEvent.setup()
    renderizar()

    fireEvent.change(screen.getByLabelText('Fecha de pago'), { target: { value: '2026-09-13' } })
    await usuario.click(screen.getByRole('button', { name: 'Registrar orden de pago' }))

    expect(screen.getByText('La fecha de pago tiene que estar entre el 14/09/2026 y hoy.')).toBeInTheDocument()
    expect(onEnviar).not.toHaveBeenCalled()
  })

  /** FR-040: el 409 lleva al paso 2 con los importes que devolvió el servidor, y Volver conserva los datos. */
  it('pasa a la confirmación con los importes del servidor y vuelve con los datos', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.clear(screen.getByLabelText('Importe'))
    await usuario.type(screen.getByLabelText('Importe'), '100000')
    await usuario.click(screen.getByRole('button', { name: 'Registrar orden de pago' }))

    expect(await screen.findByText(/Vas a registrar una orden de pago por/)).toBeInTheDocument()
    expect(screen.getByText('$ 100.000,00')).toBeInTheDocument()
    expect(screen.getByText('$ 55.000,00')).toBeInTheDocument()
    expect(onEnviar).toHaveBeenCalledWith({ fechaPago: '2026-09-20', importe: 100_000, confirmado: false })

    await usuario.click(screen.getByRole('button', { name: 'Volver' }))

    expect(screen.getByLabelText('Importe')).toHaveValue('100.000')
  })

  it('dice que la liquidación queda pagada cuando corresponde', async () => {
    onEnviar.mockResolvedValue({
      tipo: 'requiereConfirmacion',
      confirmacion: { importe: 155_000, restaPagarAntes: 155_000, restaPagarDespues: 0, quedaPagada: true },
    })
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(screen.getByRole('button', { name: 'Registrar orden de pago' }))

    expect(await screen.findByText(/Con ella la liquidación queda pagada y se cierra\./)).toBeInTheDocument()
  })

  it('confirmar reenvía con confirmado y avisa lo registrado', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(screen.getByRole('button', { name: 'Registrar orden de pago' }))
    await screen.findByText(/Vas a registrar una orden de pago por/)

    const registrada = { id: 12 }
    onEnviar.mockResolvedValue({ tipo: 'registrada', liquidacion: registrada })

    await usuario.click(screen.getByRole('button', { name: 'Confirmar orden de pago' }))

    await waitFor(() => expect(onRegistrada).toHaveBeenCalledWith(registrada))
    expect(onEnviar).toHaveBeenLastCalledWith({ fechaPago: '2026-09-20', importe: 155_000, confirmado: true })
  })
})
