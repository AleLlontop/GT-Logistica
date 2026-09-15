import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { DialogoRechazo } from './DialogoRechazo'

const onRechazar = vi.fn()
const onCancelar = vi.fn()

function renderizar() {
  render(<DialogoRechazo persona="Pérez, Juan" importe={150_000} onRechazar={onRechazar} onCancelar={onCancelar} />)
}

describe('DialogoRechazo', () => {
  beforeEach(() => {
    onRechazar.mockReset().mockResolvedValue(undefined)
    onCancelar.mockReset()
  })

  it('nombra la persona y el importe, y avisa que no se deshace', () => {
    renderizar()

    expect(screen.getByRole('dialog', { name: 'Rechazar adelanto' })).toBeInTheDocument()
    expect(screen.getByText('Pérez, Juan · $ 150.000,00')).toBeInTheDocument()
    expect(screen.getByText(/No se puede deshacer\./)).toBeInTheDocument()
  })

  /** FR-024, US4 esc. 3: sin motivo, o con sólo espacios, marca el campo y no envía. */
  it('sin motivo o con sólo espacios marca el campo y no envía', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(screen.getByRole('button', { name: 'Rechazar adelanto' }))
    expect(screen.getByText('Escribí el motivo del rechazo: queda en el historial.')).toBeInTheDocument()

    await usuario.type(screen.getByLabelText('Motivo'), '   ')
    await usuario.click(screen.getByRole('button', { name: 'Rechazar adelanto' }))

    expect(screen.getByText('Escribí el motivo del rechazo: queda en el historial.')).toBeInTheDocument()
    expect(onRechazar).not.toHaveBeenCalled()
  })

  /** FR-025: cancelar no registra nada, y eso empieza por no llamar al backend. */
  it('Volver no envía', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(screen.getByLabelText('Motivo'), 'Ya tiene otro.')
    await usuario.click(screen.getByRole('button', { name: 'Volver' }))

    expect(onCancelar).toHaveBeenCalled()
    expect(onRechazar).not.toHaveBeenCalled()
  })

  it('confirmar envía el motivo recortado', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(screen.getByLabelText('Motivo'), '  Ya tiene un adelanto pendiente del mes anterior.  ')
    await usuario.click(screen.getByRole('button', { name: 'Rechazar adelanto' }))

    await waitFor(() =>
      expect(onRechazar).toHaveBeenCalledWith('Ya tiene un adelanto pendiente del mes anterior.'),
    )
  })

  it('muestra adentro el rechazo del servidor', async () => {
    const mensaje = 'Este adelanto ya está aprobado: no se puede aprobar ni rechazar.'
    onRechazar.mockRejectedValue(new ErrorHttp(409, { codigo: 'adelanto_no_resoluble', mensaje }))
    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(screen.getByLabelText('Motivo'), 'Ya tiene otro.')
    await usuario.click(screen.getByRole('button', { name: 'Rechazar adelanto' }))

    expect(await screen.findByText(mensaje)).toHaveAttribute('role', 'alert')
  })

  /** FR-043: una acción rechazada por falta de permiso lo dice, sin sugerir reintentar. */
  it('un 403 muestra el texto de acción sin permiso', async () => {
    onRechazar.mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso.' }))
    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(screen.getByLabelText('Motivo'), 'Ya tiene otro.')
    await usuario.click(screen.getByRole('button', { name: 'Rechazar adelanto' }))

    expect(
      await screen.findByText('No tenés permiso para registrar, aprobar, rechazar ni anular adelantos.'),
    ).toHaveAttribute('role', 'alert')
  })
})
