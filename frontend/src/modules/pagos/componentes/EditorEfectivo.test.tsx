import { expect, it, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { EditorEfectivo } from './EditorEfectivo'
import type { ImporteDecimal } from '../servicios/tipos'
it('fecha servida sólo informativa, sin selector de caja y con importe exacto temporal', async () => {
  const guardar = vi.fn(); const usuario = userEvent.setup()
  render(<EditorEfectivo hoy="2026-10-18" onGuardar={guardar} onCancelar={vi.fn()} />)
  expect(screen.getByText('18/10/2026')).toBeVisible()
  expect(screen.queryByRole('combobox')).not.toBeInTheDocument()
  await usuario.type(screen.getByLabelText('Importe'), '60,01')
  await usuario.click(screen.getByRole('button', { name: 'Agregar medio' }))
  expect(guardar).toHaveBeenCalledWith(expect.objectContaining({ tipo: 'efectivo', importe: '60.01' }))
  expect(guardar.mock.calls[0][0]).not.toHaveProperty('fecha')
  expect(guardar.mock.calls[0][0]).not.toHaveProperty('cajaId')
})
it('rechaza cero y mantiene identidad del renglón al editar', async () => {
  const guardar = vi.fn(); const usuario = userEvent.setup()
  render(<EditorEfectivo hoy="2026-10-18" inicial={{ tipo: 'efectivo', renglonId: 'id-temporal', importe: '60.00' as ImporteDecimal }} onGuardar={guardar} onCancelar={vi.fn()} />)
  await usuario.clear(screen.getByLabelText('Importe')); await usuario.type(screen.getByLabelText('Importe'), '0,00')
  await usuario.click(screen.getByRole('button', { name: 'Guardar medio' }))
  expect(screen.getByLabelText('Importe')).toHaveAttribute('aria-invalid', 'true')
  expect(guardar).not.toHaveBeenCalled()
  await usuario.clear(screen.getByLabelText('Importe')); await usuario.type(screen.getByLabelText('Importe'), '50,00')
  await usuario.click(screen.getByRole('button', { name: 'Guardar medio' }))
  expect(guardar).toHaveBeenCalledWith({ tipo: 'efectivo', renglonId: 'id-temporal', importe: '50.00' })
})
