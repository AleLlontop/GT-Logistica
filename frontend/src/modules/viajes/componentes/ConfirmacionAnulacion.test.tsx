import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { ConfirmacionAnulacion } from './ConfirmacionAnulacion'

function renderizar() {
  const onConfirmar = vi.fn()
  const onCancelar = vi.fn()

  render(
    <ConfirmacionAnulacion numero={1041} onConfirmar={onConfirmar} onCancelar={onCancelar} />,
  )

  return { onConfirmar, onCancelar }
}

describe('ConfirmacionAnulacion', () => {
  /**
   * US6 esc. 2: **sin motivo escrito el botón no se habilita**. La regla se ve antes de intentar, en
   * vez de aparecer como rechazo después (FR-036).
   */
  it('sin motivo el botón de confirmar no se habilita', async () => {
    const usuario = userEvent.setup()
    renderizar()

    const confirmar = screen.getByRole('button', { name: 'Anular viaje' })
    expect(confirmar).toBeDisabled()

    // Sólo espacios tampoco alcanza: un motivo en blanco no explica nada.
    await usuario.type(screen.getByLabelText('Motivo (obligatorio)'), '   ')
    expect(confirmar).toBeDisabled()

    await usuario.type(
      screen.getByLabelText('Motivo (obligatorio)'),
      'El cliente canceló la carga.',
    )
    expect(confirmar).toBeEnabled()
  })

  /** US6 esc. 3: cancelar no modifica nada, y eso empieza por no llamar al backend. */
  it('cancelar no dispara ninguna petición', async () => {
    const usuario = userEvent.setup()
    const { onConfirmar, onCancelar } = renderizar()

    await usuario.type(
      screen.getByLabelText('Motivo (obligatorio)'),
      'El cliente canceló la carga.',
    )

    await usuario.click(screen.getByRole('button', { name: 'Cancelar' }))

    expect(onCancelar).toHaveBeenCalledOnce()
    expect(onConfirmar).not.toHaveBeenCalled()
  })

  it('confirmar entrega el motivo ya recortado', async () => {
    const usuario = userEvent.setup()
    const { onConfirmar } = renderizar()

    await usuario.type(
      screen.getByLabelText('Motivo (obligatorio)'),
      '  El cliente canceló la carga.  ',
    )

    await usuario.click(screen.getByRole('button', { name: 'Anular viaje' }))

    expect(onConfirmar).toHaveBeenCalledWith('El cliente canceló la carga.')
  })

  /** El diálogo dice las tres consecuencias que fija `contracts/README.md`. */
  it('explica qué pasa al anular', () => {
    renderizar()

    expect(screen.getByRole('heading', { name: '¿Anular el viaje 1041?' })).toBeInTheDocument()
    expect(screen.getByText(/su importe no figura en ningún total/)).toBeInTheDocument()
    expect(screen.getByText(/El chofer y el vehículo quedan libres/)).toBeInTheDocument()
    expect(screen.getByText(/no se puede volver atrás/)).toBeInTheDocument()
  })
})
