import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { DialogoAnulacion } from './DialogoAnulacion'

const onAnular = vi.fn()
const onCancelar = vi.fn()

function renderizar() {
  render(
    <DialogoAnulacion numero="LQ-12" cantidadDeViajes={3} onAnular={onAnular} onCancelar={onCancelar} />,
  )
}

describe('DialogoAnulacion', () => {
  beforeEach(() => {
    onAnular.mockReset().mockResolvedValue(undefined)
    onCancelar.mockReset()
  })

  it('dice cuántos viajes se liberan y que no se deshace', () => {
    renderizar()

    expect(screen.getByRole('dialog', { name: 'Anular liquidación LQ-12' })).toBeInTheDocument()
    expect(
      screen.getByText(
        'La liquidación queda anulada y sus 3 viajes vuelven a estar disponibles para liquidarse. No se puede deshacer.',
      ),
    ).toBeInTheDocument()
  })

  /** FR-055: sin motivo marca el campo y no envía. */
  it('sin motivo marca el campo y no envía', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(screen.getByRole('button', { name: 'Anular liquidación' }))

    expect(screen.getByText('Escribí el motivo de la anulación: queda en el historial.')).toBeInTheDocument()
    expect(onAnular).not.toHaveBeenCalled()
  })

  /** FR-056: cancelar deja todo como estaba, y eso empieza por no llamar al backend. */
  it('Volver no envía', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(screen.getByLabelText('Motivo'), 'Período equivocado.')
    await usuario.click(screen.getByRole('button', { name: 'Volver' }))

    expect(onCancelar).toHaveBeenCalled()
    expect(onAnular).not.toHaveBeenCalled()
  })

  it('confirmar envía el motivo recortado', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(screen.getByLabelText('Motivo'), '  El período no correspondía.  ')
    await usuario.click(screen.getByRole('button', { name: 'Anular liquidación' }))

    await waitFor(() => expect(onAnular).toHaveBeenCalledWith('El período no correspondía.'))
  })

  /** FR-054: el rechazo con órdenes de pago dice cuántas y por cuánto, adentro del diálogo. */
  it('muestra el rechazo con órdenes de pago', async () => {
    const mensaje = 'La liquidación LQ-12 tiene 1 orden de pago por $ 200.000,00: ya no se puede anular.'
    onAnular.mockRejectedValue(new ErrorHttp(409, { codigo: 'liquidacion_no_anulable', mensaje }))
    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(screen.getByLabelText('Motivo'), 'Período equivocado.')
    await usuario.click(screen.getByRole('button', { name: 'Anular liquidación' }))

    expect(await screen.findByText(mensaje)).toHaveAttribute('role', 'alert')
  })
})
