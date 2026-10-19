import { expect, it, vi } from 'vitest'
import { fireEvent, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { EditorTransferencia } from './EditorTransferencia'
it('conserva ceros de CBU, fecha propia y observaciones; guarda sólo estado temporal', async () => {
  const guardar = vi.fn(); const usuario = userEvent.setup()
  render(<EditorTransferencia hoy="2026-10-18" onGuardar={guardar} onCancelar={vi.fn()} />)
  fireEvent.change(screen.getByLabelText('Fecha'), { target: { value: '2026-10-17' } })
  await usuario.type(screen.getByLabelText('CBU origen'), '0000000000000000000001')
  await usuario.type(screen.getByLabelText('CBU destino'), '0000000000000000000002')
  await usuario.type(screen.getByLabelText('Número de comprobante'), 'C-001')
  await usuario.type(screen.getByLabelText('Importe'), '250,01')
  await usuario.type(screen.getByLabelText('Observaciones'), 'Pago externo')
  await usuario.click(screen.getByRole('button', { name: 'Agregar medio' }))
  expect(guardar).toHaveBeenCalledWith(expect.objectContaining({ tipo: 'transferencia', fecha: '2026-10-17', cbuOrigen: '0000000000000000000001',
    cbuDestino: '0000000000000000000002', numeroComprobante: 'C-001', importe: '250.01', observaciones: 'Pago externo' }))
})
it('rechaza CBU con letras/longitud inválida, fecha futura y comprobante ausente sin modificar contenido', async () => {
  const guardar = vi.fn(); const usuario = userEvent.setup()
  render(<EditorTransferencia hoy="2026-10-18" onGuardar={guardar} onCancelar={vi.fn()} />)
  fireEvent.change(screen.getByLabelText('Fecha'), { target: { value: '2026-10-19' } })
  await usuario.type(screen.getByLabelText('CBU origen'), 'A'.repeat(22))
  await usuario.type(screen.getByLabelText('CBU destino'), '0'.repeat(21))
  await usuario.click(screen.getByRole('button', { name: 'Agregar medio' }))
  expect(guardar).not.toHaveBeenCalled()
  expect(screen.getByLabelText('CBU origen')).toHaveValue('A'.repeat(22))
  expect(screen.getByLabelText('CBU destino')).toHaveAttribute('aria-invalid', 'true')
  expect(screen.getByLabelText('Fecha')).toHaveAttribute('aria-invalid', 'true')
  expect(screen.getByLabelText('Número de comprobante')).toHaveAttribute('aria-invalid', 'true')
})
