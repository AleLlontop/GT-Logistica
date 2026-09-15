import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ErrorHttp } from '../../../compartido/clienteHttp'
import { DialogoAnulacion } from './DialogoAnulacion'

const onAnular = vi.fn()
const onCancelar = vi.fn()

function renderizar() {
  render(<DialogoAnulacion persona="Pérez, Juan" importe={150_000} onAnular={onAnular} onCancelar={onCancelar} />)
}

describe('DialogoAnulacion (adelantos)', () => {
  beforeEach(() => {
    onAnular.mockReset().mockResolvedValue(undefined)
    onCancelar.mockReset()
  })

  it('dice qué se anula, que deja de sumar y que no se deshace', () => {
    renderizar()

    expect(screen.getByRole('dialog', { name: 'Anular adelanto' })).toHaveTextContent(
      'El adelanto de $ 150.000,00 para Pérez, Juan queda anulado y deja de sumar en el total adelantado. No se puede deshacer.',
    )
  })

  /** FR-029, US5 esc. 3: sin motivo, o con sólo espacios, marca el campo y no anula. */
  it('sin motivo o con sólo espacios marca el campo y no envía', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.click(screen.getByRole('button', { name: 'Anular adelanto' }))
    expect(screen.getByText('Escribí el motivo de la anulación: queda en el historial.')).toBeInTheDocument()

    await usuario.type(screen.getByLabelText('Motivo'), '   ')
    await usuario.click(screen.getByRole('button', { name: 'Anular adelanto' }))

    expect(screen.getByText('Escribí el motivo de la anulación: queda en el historial.')).toBeInTheDocument()
    expect(onAnular).not.toHaveBeenCalled()
  })

  /** FR-030: cancelar deja el adelanto aprobado, y eso empieza por no llamar al backend. */
  it('Volver no envía', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(screen.getByLabelText('Motivo'), 'Persona equivocada.')
    await usuario.click(screen.getByRole('button', { name: 'Volver' }))

    expect(onCancelar).toHaveBeenCalled()
    expect(onAnular).not.toHaveBeenCalled()
  })

  it('confirmar envía el motivo recortado', async () => {
    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(screen.getByLabelText('Motivo'), '  Cargado sobre la persona equivocada.  ')
    await usuario.click(screen.getByRole('button', { name: 'Anular adelanto' }))

    await waitFor(() => expect(onAnular).toHaveBeenCalledWith('Cargado sobre la persona equivocada.'))
  })

  it('muestra adentro el rechazo adelanto_no_anulable', async () => {
    const mensaje = 'Este adelanto ya está anulado.'
    onAnular.mockRejectedValue(new ErrorHttp(409, { codigo: 'adelanto_no_anulable', mensaje }))
    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(screen.getByLabelText('Motivo'), 'Persona equivocada.')
    await usuario.click(screen.getByRole('button', { name: 'Anular adelanto' }))

    expect(await screen.findByText(mensaje)).toHaveAttribute('role', 'alert')
  })

  it('un 403 muestra el texto de acción sin permiso', async () => {
    onAnular.mockRejectedValue(new ErrorHttp(403, { codigo: 'sin_permiso', mensaje: 'Sin permiso.' }))
    const usuario = userEvent.setup()
    renderizar()

    await usuario.type(screen.getByLabelText('Motivo'), 'Persona equivocada.')
    await usuario.click(screen.getByRole('button', { name: 'Anular adelanto' }))

    expect(
      await screen.findByText('No tenés permiso para registrar, aprobar, rechazar ni anular adelantos.'),
    ).toHaveAttribute('role', 'alert')
  })
})
